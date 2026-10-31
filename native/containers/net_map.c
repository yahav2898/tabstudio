/* TabStudio's container network for PRoot.
 *
 * Android gives an app no network namespace, so every container shares the device's network. This extension gives
 * each container its own loopback address (127.42.x.y) and rewrites the socket addresses its programs use, so two
 * containers can both listen on 5432, or on 80, which an app may not bind, and still reach each other by name:
 *
 *   --net-map=RULES   RULES is a comma-separated list; the option may be repeated.
 *     A<addr>                       The container's own address. A bind(2) to a wildcard or loopback address
 *                                   (0.0.0.0, 127.0.0.1, ::, ::1) on a port no P rule names binds this address
 *                                   instead, and connect(2) to localhost reaches it.
 *     P<port>=<host-port>           A published port: a wildcard or loopback bind of <port> binds 127.0.0.1:<host-port>
 *                                   (for IPv6 [::1], or ::ffff:127.0.0.1 on a dual-stack socket), so the device and
 *                                   the IDE reach it on localhost, and the container's own connections to
 *                                   localhost:<port> go there too.
 *     W<port>=<host-port>           The same, but a wildcard bind stays a wildcard: published on every interface.
 *     C<addr>:<port>=<addr>:<port>  connect(2) and sendto(2) to this address go to the other one; a port of * matches
 *                                   any port and keeps it. IPv4-mapped IPv6 addresses (::ffff:a.b.c.d) match too.
 *                                   C[<v6>]:<port>=[<v6>]:<port> is the IPv6 form.
 *     @<file>                       More rules, one per line, read from a file and read again when it changes:
 *                                   the other containers' addresses, which change as containers start and stop.
 *
 * A wildcard IPv6 socket the program made IPv6-only (setsockopt IPV6_V6ONLY, as nginx, PostgreSQL and Redis do next
 * to an IPv4 socket) gets [::1]; any other gets the IPv4-mapped form, so a dual-stack listener (Node.js, Java, Go)
 * still answers on IPv4.
 *
 * Containers also get setgroups(2) as a success where Android refuses it (EPERM): su-exec, gosu and setpriv call it
 * when an image's entrypoint drops root, and under PRoot's simulated root there are no real groups to change.
 *
 * Every rewritten address is copied to the tracee's stack (set_sysarg_data), so the program's own buffer keeps the
 * address it asked for. Licensed like PRoot, GPL-2.0-or-later. */

#include <sys/socket.h>
#include <sys/stat.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <errno.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <talloc.h>

#include "extension/extension.h"
#include "tracee/tracee.h"
#include "tracee/mem.h"
#include "syscall/syscall.h"
#include "syscall/sysnum.h"
#include "cli/note.h"

typedef enum { RULE_PORT, RULE_WIDE_PORT, RULE_CONNECT } RuleKind;

typedef struct {
	RuleKind kind;
	int family;            /* Connect rules: AF_INET or AF_INET6. */
	uint8_t from_addr[16];
	uint16_t from_port;    /* 0: any port. */
	uint8_t to_addr[16];
	uint16_t to_port;      /* 0: the same port. */
} NetMapRule;

typedef struct {
	pid_t pid;
	int fd;
} V6OnlySocket;

typedef struct {
	NetMapRule *rules;
	size_t count;
	bool has_address;
	uint8_t address[4];    /* The container's own IPv4 address. */
	char *file;
	struct timespec file_mtime;
	NetMapRule *file_rules;
	size_t file_count;
	V6OnlySocket v6only[64]; /* Recent IPV6_V6ONLY sockets, consumed by their bind(2). */
	size_t v6only_next;
} NetMapConfig;

static const uint8_t v4_mapped_prefix[12] = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xff, 0xff };
static const uint8_t v6_loopback[16] = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
static const uint8_t v6_any[16] = { 0 };
static const uint8_t v4_loopback[4] = { 127, 0, 0, 1 };

static bool parse_port(const char *text, uint16_t *port, bool allow_any)
{
	char *end;
	long value;
	if (allow_any && strcmp(text, "*") == 0) {
		*port = 0;
		return true;
	}
	value = strtol(text, &end, 10);
	if (end == text || *end != '\0' || value < 1 || value > 65535)
		return false;
	*port = (uint16_t) value;
	return true;
}

/* Parses "a.b.c.d:port" or "[v6]:port". */
static bool parse_endpoint(char *text, int *family, uint8_t addr[16], uint16_t *port)
{
	char *colon;
	memset(addr, 0, 16);
	if (text[0] == '[') {
		char *close = strchr(text, ']');
		if (close == NULL || close[1] != ':')
			return false;
		*close = '\0';
		*family = AF_INET6;
		return inet_pton(AF_INET6, text + 1, addr) == 1 && parse_port(close + 2, port, true);
	}
	colon = strrchr(text, ':');
	if (colon == NULL)
		return false;
	*colon = '\0';
	*family = AF_INET;
	return inet_pton(AF_INET, text, addr) == 1 && parse_port(colon + 1, port, true);
}

/* Parses one rule into @rule; returns 1 for a rule, 0 for something handled here (A, @), -1 for an error. */
static int parse_rule(NetMapConfig *config, char *item, NetMapRule *rule)
{
	char *equals;
	memset(rule, 0, sizeof(*rule));
	if (item[0] == 'A')
		return inet_pton(AF_INET, item + 1, config->address) == 1 ? (config->has_address = true, 0) : -1;
	if (item[0] == '@') {
		config->file = talloc_strdup(config, item + 1);
		return config->file != NULL ? 0 : -1;
	}
	equals = strchr(item, '=');
	if (equals == NULL)
		return -1;
	*equals = '\0';
	if (item[0] == 'P' || item[0] == 'W') {
		rule->kind = item[0] == 'P' ? RULE_PORT : RULE_WIDE_PORT;
		return parse_port(item + 1, &rule->from_port, false) && parse_port(equals + 1, &rule->to_port, false) ? 1 : -1;
	}
	if (item[0] == 'C') {
		int to_family;
		rule->kind = RULE_CONNECT;
		return parse_endpoint(item + 1, &rule->family, rule->from_addr, &rule->from_port)
		       && parse_endpoint(equals + 1, &to_family, rule->to_addr, &rule->to_port)
		       && to_family == rule->family ? 1 : -1;
	}
	return -1;
}

static int add_rule(TALLOC_CTX *owner, NetMapRule **rules, size_t *count, const NetMapRule *rule)
{
	*rules = talloc_realloc(owner, *rules, NetMapRule, *count + 1);
	if (*rules == NULL)
		return -1;
	(*rules)[(*count)++] = *rule;
	return 0;
}

static int parse_rules(NetMapConfig *config, const char *cli)
{
	char *copy = talloc_strdup(config, cli);
	char *save = NULL;
	char *item;
	if (copy == NULL)
		return -1;
	for (item = strtok_r(copy, ",", &save); item != NULL; item = strtok_r(NULL, ",", &save)) {
		NetMapRule rule;
		int status = parse_rule(config, item, &rule);
		if (status < 0) {
			note(NULL, ERROR, USER, "--net-map: invalid rule '%s'", item);
			return -1;
		}
		if (status > 0 && add_rule(config, &config->rules, &config->count, &rule) < 0)
			return -1;
	}
	return 0;
}

/* Reads the rule file again when it changed; a rule file that is missing or invalid gives no file rules. */
static void reload_file(NetMapConfig *config)
{
	struct stat status;
	FILE *file;
	char line[256];
	if (config->file == NULL)
		return;
	if (stat(config->file, &status) < 0) {
		config->file_count = 0;
		return;
	}
	if (status.st_mtim.tv_sec == config->file_mtime.tv_sec && status.st_mtim.tv_nsec == config->file_mtime.tv_nsec)
		return;
	config->file_mtime = status.st_mtim;
	config->file_count = 0;
	file = fopen(config->file, "re");
	if (file == NULL)
		return;
	while (fgets(line, sizeof(line), file) != NULL) {
		NetMapRule rule;
		NetMapConfig scratch;
		line[strcspn(line, "\r\n")] = '\0';
		if (line[0] != 'C')
			continue; /* Only connect rules come from the file: binds belong to the container itself. */
		memset(&scratch, 0, sizeof(scratch));
		if (parse_rule(&scratch, line, &rule) == 1)
			(void) add_rule(config, &config->file_rules, &config->file_count, &rule);
	}
	fclose(file);
}

static bool is_loopback_or_any4(const uint8_t addr[4])
{
	return addr[0] == 127 || (addr[0] == 0 && addr[1] == 0 && addr[2] == 0 && addr[3] == 0);
}

static void set_mapped(uint8_t bytes[16], const uint8_t v4[4])
{
	memcpy(bytes, v4_mapped_prefix, 12);
	memcpy(bytes + 12, v4, 4);
}

static const NetMapRule *find_port(const NetMapConfig *config, uint16_t port)
{
	size_t i;
	for (i = 0; i < config->count; i++)
		if (config->rules[i].kind != RULE_CONNECT && config->rules[i].from_port == port)
			return &config->rules[i];
	return NULL;
}

/* bind(2): returns true when @address changed. */
static bool rewrite_bind(const NetMapConfig *config, bool v6only, struct sockaddr_storage *address, size_t length)
{
	if (address->ss_family == AF_INET && length >= sizeof(struct sockaddr_in)) {
		struct sockaddr_in *in = (struct sockaddr_in *) address;
		uint8_t *bytes = (uint8_t *) &in->sin_addr;
		const NetMapRule *rule = find_port(config, ntohs(in->sin_port));
		bool any = in->sin_addr.s_addr == htonl(INADDR_ANY);
		if (!is_loopback_or_any4(bytes) || ntohs(in->sin_port) == 0)
			return false;
		if (rule != NULL) {
			in->sin_port = htons(rule->to_port);
			if (!(rule->kind == RULE_WIDE_PORT && any))
				memcpy(bytes, v4_loopback, 4);
			return true;
		}
		if (config->has_address) {
			memcpy(bytes, config->address, 4);
			return true;
		}
		return false;
	}
	if (address->ss_family == AF_INET6 && length >= sizeof(struct sockaddr_in6)) {
		struct sockaddr_in6 *in6 = (struct sockaddr_in6 *) address;
		uint8_t *bytes = in6->sin6_addr.s6_addr;
		bool any = memcmp(bytes, v6_any, 16) == 0;
		bool loopback = memcmp(bytes, v6_loopback, 16) == 0;
		bool mapped = memcmp(bytes, v4_mapped_prefix, 12) == 0 && is_loopback_or_any4(bytes + 12);
		const NetMapRule *rule = find_port(config, ntohs(in6->sin6_port));
		if (!(any || loopback || mapped) || ntohs(in6->sin6_port) == 0)
			return false;
		if (rule != NULL) {
			in6->sin6_port = htons(rule->to_port);
			if (rule->kind == RULE_WIDE_PORT && any)
				return true;
			if (loopback || (any && v6only))
				memcpy(bytes, v6_loopback, 16);
			else
				set_mapped(bytes, v4_loopback);
			return true;
		}
		if (!config->has_address || loopback)
			return false;
		if (any && v6only)
			memcpy(bytes, v6_loopback, 16); /* An IPv6-only listener stays on the device, not the network. */
		else
			set_mapped(bytes, config->address);
		return true;
	}
	return false;
}

static bool match_connect(const NetMapRule *rule, int family, const uint8_t *addr, uint16_t port)
{
	return rule->kind == RULE_CONNECT && rule->family == family && (rule->from_port == 0 || rule->from_port == port)
	       && memcmp(rule->from_addr, addr, family == AF_INET ? 4 : 16) == 0;
}

/* Applies the first matching connect rule to a v4 (or v4-mapped) destination. */
static bool connect_v4(const NetMapConfig *config, uint8_t addr[4], uint16_t *port)
{
	size_t i;
	const NetMapRule *rule;
	const NetMapRule *lists[2] = { config->rules, config->file_rules };
	size_t counts[2] = { config->count, config->file_count };
	size_t list;
	/* The container's own published ports, reached through localhost. */
	if (addr[0] == 127 && config->has_address && (memcmp(addr, v4_loopback, 4) == 0 || memcmp(addr, config->address, 4) == 0)) {
		rule = find_port(config, *port);
		if (rule != NULL) {
			memcpy(addr, v4_loopback, 4);
			*port = rule->to_port;
			return true;
		}
	}
	for (list = 0; list < 2; list++) {
		for (i = 0; i < counts[list]; i++) {
			rule = &lists[list][i];
			if (match_connect(rule, AF_INET, addr, *port)) {
				memcpy(addr, rule->to_addr, 4);
				if (rule->to_port != 0)
					*port = rule->to_port;
				return true;
			}
		}
	}
	/* Anything else on localhost is the container itself. */
	if (config->has_address && memcmp(addr, v4_loopback, 4) == 0) {
		memcpy(addr, config->address, 4);
		return true;
	}
	return false;
}

static bool rewrite_connect(const NetMapConfig *config, struct sockaddr_storage *address, size_t length)
{
	size_t i;
	if (address->ss_family == AF_INET && length >= sizeof(struct sockaddr_in)) {
		struct sockaddr_in *in = (struct sockaddr_in *) address;
		uint16_t port = ntohs(in->sin_port);
		if (!connect_v4(config, (uint8_t *) &in->sin_addr, &port))
			return false;
		in->sin_port = htons(port);
		return true;
	}
	if (address->ss_family == AF_INET6 && length >= sizeof(struct sockaddr_in6)) {
		struct sockaddr_in6 *in6 = (struct sockaddr_in6 *) address;
		uint8_t *bytes = in6->sin6_addr.s6_addr;
		uint16_t port = ntohs(in6->sin6_port);
		if (memcmp(bytes, v4_mapped_prefix, 12) == 0) {
			if (!connect_v4(config, bytes + 12, &port))
				return false;
			in6->sin6_port = htons(port);
			return true;
		}
		for (i = 0; i < config->count + config->file_count; i++) {
			const NetMapRule *rule = i < config->count ? &config->rules[i] : &config->file_rules[i - config->count];
			if (match_connect(rule, AF_INET6, bytes, port)) {
				memcpy(bytes, rule->to_addr, 16);
				if (rule->to_port != 0)
					in6->sin6_port = htons(rule->to_port);
				return true;
			}
		}
		if (memcmp(bytes, v6_loopback, 16) == 0 && config->has_address) {
			/* [::1] in a container is the container: its published port, or its own address. */
			const NetMapRule *rule = find_port(config, port);
			if (rule != NULL) {
				set_mapped(bytes, v4_loopback);
				in6->sin6_port = htons(rule->to_port);
			} else {
				set_mapped(bytes, config->address);
			}
			return true;
		}
	}
	return false;
}

/* Whether this tracee made socket @fd IPv6-only; the entry is consumed. */
static bool take_v6only(NetMapConfig *config, pid_t pid, int fd)
{
	size_t i;
	for (i = 0; i < sizeof(config->v6only) / sizeof(config->v6only[0]); i++) {
		if (config->v6only[i].pid == pid && config->v6only[i].fd == fd) {
			config->v6only[i].pid = 0;
			return true;
		}
	}
	return false;
}

static void note_setsockopt(Tracee *tracee, NetMapConfig *config)
{
	int value = 0;
	int fd = (int) peek_reg(tracee, CURRENT, SYSARG_1);
	size_t slot;
	if ((int) peek_reg(tracee, CURRENT, SYSARG_2) != IPPROTO_IPV6
	    || (int) peek_reg(tracee, CURRENT, SYSARG_3) != IPV6_V6ONLY
	    || peek_reg(tracee, CURRENT, SYSARG_5) < sizeof(int)
	    || read_data(tracee, &value, peek_reg(tracee, CURRENT, SYSARG_4), sizeof(int)) < 0)
		return;
	(void) take_v6only(config, tracee->pid, fd);
	if (value == 0)
		return;
	slot = config->v6only_next++ % (sizeof(config->v6only) / sizeof(config->v6only[0]));
	config->v6only[slot].pid = tracee->pid;
	config->v6only[slot].fd = fd;
}

/* Reads the address argument @address_reg (length in @length_reg), rewrites it, and points the argument at the
 * rewritten copy. */
static int rewrite(Tracee *tracee, NetMapConfig *config, bool connect, Reg address_reg, Reg length_reg)
{
	struct sockaddr_storage address;
	word_t pointer = peek_reg(tracee, CURRENT, address_reg);
	word_t length = peek_reg(tracee, CURRENT, length_reg);
	bool changed;

	if (pointer == 0 || length < sizeof(sa_family_t) || length > sizeof(address))
		return 0;
	memset(&address, 0, sizeof(address));
	if (read_data(tracee, &address, pointer, length) < 0)
		return 0; /* Let the kernel report EFAULT itself. */
	if (address.ss_family != AF_INET && address.ss_family != AF_INET6)
		return 0;

	if (connect) {
		reload_file(config);
		changed = rewrite_connect(config, &address, length);
	} else {
		changed = rewrite_bind(config, take_v6only(config, tracee->pid, (int) peek_reg(tracee, CURRENT, SYSARG_1)), &address, length);
	}
	if (!changed)
		return 0;
	return set_sysarg_data(tracee, &address, length, address_reg) < 0 ? -EFAULT : 0;
}

int net_map_callback(Extension *extension, ExtensionEvent event, intptr_t data1, intptr_t data2 UNUSED)
{
	switch (event) {
	case INITIALIZATION: {
		static FilteredSysnum filtered_sysnums[] = {
			{ PR_bind, 0 },
			{ PR_connect, 0 },
			{ PR_sendto, 0 },
			{ PR_setsockopt, 0 },
			{ PR_setgroups, FILTER_SYSEXIT },
			FILTERED_SYSNUM_END
		};
		NetMapConfig *config = talloc_zero(extension, NetMapConfig);
		if (config == NULL)
			return -1;
		extension->config = config;
		extension->filtered_sysnums = filtered_sysnums;
		return parse_rules(config, (const char *) data1);
	}

	case INHERIT_PARENT:
		/* One configuration for the whole container: children share it. */
		return 0;

	case SYSCALL_ENTER_END: {
		Tracee *tracee = TRACEE(extension);
		NetMapConfig *config = talloc_get_type_abort(extension->config, NetMapConfig);
		switch (get_sysnum(tracee, ORIGINAL)) {
		case PR_bind:
			return rewrite(tracee, config, false, SYSARG_2, SYSARG_3);
		case PR_connect:
			return rewrite(tracee, config, true, SYSARG_2, SYSARG_3);
		case PR_sendto:
			return rewrite(tracee, config, true, SYSARG_5, SYSARG_6);
		case PR_setsockopt:
			note_setsockopt(tracee, config);
			return 0;
		default:
			return 0;
		}
	}

	case SYSCALL_EXIT_END: {
		Tracee *tracee = TRACEE(extension);
		if (get_sysnum(tracee, ORIGINAL) == PR_setgroups && (int) peek_reg(tracee, CURRENT, SYSARG_RESULT) == -EPERM)
			poke_reg(tracee, SYSARG_RESULT, 0);
		return 0;
	}

	default:
		return 0;
	}
}
