/* TabStudio's network map for PRoot: published ports and service addresses for containers.
 *
 * Android gives an app no network namespace, so every container shares the device's network. This extension
 * rewrites the socket addresses a container's programs use, so two containers that both listen on 5432 (or on 80,
 * which an app may not bind) can run side by side:
 *
 *   --net-map=RULES   RULES is a comma-separated list, and the option may be repeated.
 *     B<port>=<port>                  bind(2) of any IPv4/IPv6 address with this port binds the new port instead.
 *     C<addr>:<port>=<addr>:<port>    connect(2) and sendto(2) to this IPv4 address and port go to the new one.
 *                                     IPv4-mapped IPv6 addresses (::ffff:a.b.c.d) match the same rules.
 *     C[<addr6>]:<port>=[<addr6>]:<port>   the same for an IPv6 address such as [::1].
 *
 * The rewritten address is copied to the tracee's stack (set_sysarg_data), so the program's own buffer keeps the
 * address it asked for. A program that binds port 0, or an address no rule names, is left alone.
 * Licensed like PRoot, GPL-2.0-or-later. */

#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <stdint.h>
#include <stdlib.h>
#include <string.h>
#include <talloc.h>

#include "extension/extension.h"
#include "tracee/tracee.h"
#include "tracee/mem.h"
#include "syscall/syscall.h"
#include "syscall/sysnum.h"
#include "cli/note.h"

typedef struct {
	bool connect;          /* false: a bind rule (port only); true: a connect/sendto rule (address and port). */
	int family;            /* AF_INET or AF_INET6, for connect rules. */
	uint8_t from_addr[16];
	uint16_t from_port;
	uint8_t to_addr[16];
	uint16_t to_port;
} NetMapRule;

typedef struct {
	NetMapRule *rules;
	size_t count;
} NetMapConfig;

static bool parse_port(const char *text, uint16_t *port)
{
	char *end;
	long value = strtol(text, &end, 10);
	if (end == text || *end != '\0' || value < 1 || value > 65535)
		return false;
	*port = (uint16_t) value;
	return true;
}

/* Parses "a.b.c.d:port" or "[v6]:port". */
static bool parse_endpoint(char *text, int *family, uint8_t addr[16], uint16_t *port)
{
	char *colon;
	if (text[0] == '[') {
		char *close = strchr(text, ']');
		if (close == NULL || close[1] != ':')
			return false;
		*close = '\0';
		*family = AF_INET6;
		return inet_pton(AF_INET6, text + 1, addr) == 1 && parse_port(close + 2, port);
	}
	colon = strrchr(text, ':');
	if (colon == NULL)
		return false;
	*colon = '\0';
	*family = AF_INET;
	memset(addr, 0, 16);
	return inet_pton(AF_INET, text, addr) == 1 && parse_port(colon + 1, port);
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
		char *equals = strchr(item, '=');
		memset(&rule, 0, sizeof(rule));
		if (equals == NULL || (item[0] != 'B' && item[0] != 'C'))
			goto invalid;
		*equals = '\0';
		if (item[0] == 'B') {
			if (!parse_port(item + 1, &rule.from_port) || !parse_port(equals + 1, &rule.to_port))
				goto invalid;
		} else {
			int to_family;
			rule.connect = true;
			if (!parse_endpoint(item + 1, &rule.family, rule.from_addr, &rule.from_port)
			    || !parse_endpoint(equals + 1, &to_family, rule.to_addr, &rule.to_port)
			    || to_family != rule.family)
				goto invalid;
		}
		config->rules = talloc_realloc(config, config->rules, NetMapRule, config->count + 1);
		if (config->rules == NULL)
			return -1;
		config->rules[config->count++] = rule;
		continue;
invalid:
		note(NULL, ERROR, USER, "--net-map: invalid rule '%s'", item);
		return -1;
	}
	return 0;
}

static const uint8_t v4_mapped_prefix[12] = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0xff, 0xff };

/* Rewrites @address in place; returns true when a rule matched. */
static bool apply_rules(const NetMapConfig *config, bool connect, struct sockaddr_storage *address, size_t length)
{
	size_t i;
	if (address->ss_family == AF_INET && length >= sizeof(struct sockaddr_in)) {
		struct sockaddr_in *in = (struct sockaddr_in *) address;
		uint16_t port = ntohs(in->sin_port);
		for (i = 0; i < config->count; i++) {
			const NetMapRule *rule = &config->rules[i];
			if (rule->connect != connect || rule->from_port != port)
				continue;
			if (!connect) {
				in->sin_port = htons(rule->to_port);
				return true;
			}
			if (rule->family == AF_INET && memcmp(&in->sin_addr, rule->from_addr, 4) == 0) {
				memcpy(&in->sin_addr, rule->to_addr, 4);
				in->sin_port = htons(rule->to_port);
				return true;
			}
		}
	} else if (address->ss_family == AF_INET6 && length >= sizeof(struct sockaddr_in6)) {
		struct sockaddr_in6 *in6 = (struct sockaddr_in6 *) address;
		uint8_t *bytes = in6->sin6_addr.s6_addr;
		uint16_t port = ntohs(in6->sin6_port);
		bool mapped = memcmp(bytes, v4_mapped_prefix, 12) == 0;
		for (i = 0; i < config->count; i++) {
			const NetMapRule *rule = &config->rules[i];
			if (rule->connect != connect || rule->from_port != port)
				continue;
			if (!connect) {
				in6->sin6_port = htons(rule->to_port);
				return true;
			}
			if (rule->family == AF_INET6 && memcmp(bytes, rule->from_addr, 16) == 0) {
				memcpy(bytes, rule->to_addr, 16);
				in6->sin6_port = htons(rule->to_port);
				return true;
			}
			if (rule->family == AF_INET && mapped && memcmp(bytes + 12, rule->from_addr, 4) == 0) {
				memcpy(bytes + 12, rule->to_addr, 4);
				in6->sin6_port = htons(rule->to_port);
				return true;
			}
		}
	}
	return false;
}

/* Reads the address argument @address_reg (length in @length_reg), applies the rules, and points the argument at
 * a rewritten copy. */
static int rewrite(Tracee *tracee, const NetMapConfig *config, bool connect, Reg address_reg, Reg length_reg)
{
	struct sockaddr_storage address;
	word_t pointer = peek_reg(tracee, CURRENT, address_reg);
	word_t length = peek_reg(tracee, CURRENT, length_reg);
	int status;

	if (pointer == 0 || length < sizeof(sa_family_t) || length > sizeof(address))
		return 0;

	memset(&address, 0, sizeof(address));
	status = read_data(tracee, &address, pointer, length);
	if (status < 0)
		return 0; /* Let the kernel report EFAULT itself. */

	if (!apply_rules(config, connect, &address, length))
		return 0;

	status = set_sysarg_data(tracee, &address, length, address_reg);
	return status < 0 ? status : 0;
}

int net_map_callback(Extension *extension, ExtensionEvent event, intptr_t data1, intptr_t data2 UNUSED)
{
	switch (event) {
	case INITIALIZATION: {
		static FilteredSysnum filtered_sysnums[] = {
			{ PR_bind, 0 },
			{ PR_connect, 0 },
			{ PR_sendto, 0 },
			FILTERED_SYSNUM_END
		};
		NetMapConfig *config;

		config = talloc_zero(extension, NetMapConfig);
		if (config == NULL)
			return -1;
		extension->config = config;
		extension->filtered_sysnums = filtered_sysnums;
		return parse_rules(config, (const char *) data1);
	}

	case INHERIT_PARENT:
		/* The rules never change after start: children share them. */
		return 0;

	case SYSCALL_ENTER_END: {
		Tracee *tracee = TRACEE(extension);
		const NetMapConfig *config = talloc_get_type_abort(extension->config, NetMapConfig);
		if (config->count == 0)
			return 0;
		switch (get_sysnum(tracee, ORIGINAL)) {
		case PR_bind:
			return rewrite(tracee, config, false, SYSARG_2, SYSARG_3);
		case PR_connect:
			return rewrite(tracee, config, true, SYSARG_2, SYSARG_3);
		case PR_sendto:
			return rewrite(tracee, config, true, SYSARG_5, SYSARG_6);
		default:
			return 0;
		}
	}

	default:
		return 0;
	}
}
