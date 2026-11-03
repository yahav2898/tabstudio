#!/usr/bin/env python3
"""Compares the squiggles TabStudio's language servers draw with the errors each language's compiler reports.

The Real repositories jobs open every TypeScript, Go, Rust, Java and Python repository of build/ci/real-repos.json in
the IDE (RealRepositoryTests), open some of its source files, wait for their language servers, and save the editor's
errors. This script chooses those files, runs the compiler on the same files, compares the two per file and line, and
gates the result against each repository's baseline.

Usage: build/ci/squiggles.py files <toolchain> <folder>
       build/ci/squiggles.py compare --name <name> --toolchain <toolchain> --folder <folder> --squiggles <json> --out <dir>
       build/ci/squiggles.py summary --out <dir>

What "the compiler" is, per language (the toolchain names are real-repos.json's):

  node    TypeScript: tsc --noEmit -p for every tsconfig.json and the projects it references, with the project's own
          tsc from node_modules when it has one. A file is compared with the program tsserver gives it: the nearest
          tsconfig.json that includes it, or one of its references. Repositories without a tsconfig.json are not
          compared, and neither are files no tsconfig includes, which tsserver checks with defaults of its own.
          TS6305 is not an error in the code: it says a referenced project's output was not built, as tsc -b would.
  go      go build ./... and the type errors of go vet ./... (its "vet:" lines, which include test files), in every
          module; vet's analyzer findings are not compiler errors, and gopls shows them as warnings.
  rust    cargo check --workspace --all-targets --keep-going --message-format=json, as rust-analyzer's own check
          runs it; files are compared when rustc read them (its dep-info files) or reported errors in them.
  java    javac through the project's build: mvn test-compile (or Gradle's compileJava compileTestJava), with the
          build's checks that are not the compiler (Checkstyle, the enforcer, formatters) skipped.
  python  python -m py_compile's syntax errors, with the project's own .venv interpreter when it has one, plus the
          Pyright command line where the project configures Pyright (pyrightconfig.json or [tool.pyright]), with
          the configuration and .venv of the folder the IDE's Pyright serves the file from (its nearest pyproject.toml,
          requirements.txt or other root marker). Pyright is the only type checker the IDE runs, so a project
          that configures only mypy is compared with py_compile alone: Python's compiler accepts what mypy rejects.
          CPython stops at a file's first syntax error, so a file with one agrees when the editor shows any error.

An editor error on a line without a compiler error is a false squiggle; a compiler error on a line without an editor
error is a missed one. Each repository's differences may not exceed its baseline (real-repos.json's
"squiggleBaselines", 0 when it has none), and fewer ask for the lower number to be recorded, as maxErrors does.
"""
import argparse
import json
import os
import pathlib
import re
import shutil
import subprocess
import sys

try:
    import tomllib
except ImportError:  # Python before 3.11
    tomllib = None

FILES_PER_REPOSITORY = 12
COMMAND_TIMEOUT = 20 * 60

EXTENSIONS = {
    "node": (".ts", ".tsx", ".mts", ".cts"),
    "go": (".go",),
    "rust": (".rs",),
    "java": (".java",),
    "python": (".py",),
}

LANGUAGE_NAMES = {"node": "TypeScript", "go": "Go", "rust": "Rust", "java": "Java", "python": "Python"}

SKIPPED_FOLDERS = {
    "node_modules", ".venv", "venv", "env", "__pycache__", "site-packages", "target", "build", "dist", "out",
    "vendor", "testdata", "bin", "obj",
}

TEST_FOLDERS = {"test", "tests", "testing", "__tests__", "spec", "e2e", "benches", "bench", "benchmarks", "examples"}

# Python's compile(), as py_compile runs it, on each file named in argv; prints the syntax errors as JSON.
PY_COMPILE = """
import json, sys
errors = []
for path in sys.argv[1:]:
    try:
        with open(path, 'rb') as source:
            compile(source.read(), path, 'exec', dont_inherit=True)
    except SyntaxError as error:
        errors.append({'file': path, 'line': error.lineno or 1, 'column': error.offset or 1, 'code': type(error).__name__, 'message': error.msg})
    except ValueError as error:
        errors.append({'file': path, 'line': 1, 'column': 1, 'code': 'ValueError', 'message': str(error)})
print(json.dumps(errors))
"""

TSC_ERROR = re.compile(r"^(?P<file>[^\s(][^(]*)\((?P<line>\d+),(?P<column>\d+)\): error (?P<code>TS\d+): (?P<message>.*)$")
GO_ERROR = re.compile(r"^(?:vet: )?(?P<file>[^\s:#][^:]*\.go):(?P<line>\d+):(?P<column>\d+): (?P<message>.*)$")
MAVEN_ERROR = re.compile(r"^\[ERROR\] (?P<file>.+?\.java):\[(?P<line>\d+),(?P<column>\d+)\] (?P<message>.*)$")
GRADLE_ERROR = re.compile(r"^(?P<file>.+?\.java):(?P<line>\d+): error: (?P<message>.*)$")


class CompilerResult:
    """The errors a compiler reported, by file, and the files it compiled (None: every file it was given)."""

    def __init__(self):
        self.errors = {}
        self.covered = set()
        self.covers_all = False
        self.commands = []
        self.problems = []
        # Files whose errors cannot be compared line by line, such as Python's first syntax error.
        self.whole_file = set()

    def add(self, file, line, column, code, message):
        file = os.path.normpath(file)
        error = {"line": int(line), "column": int(column), "code": code, "message": message.strip()}
        errors = self.errors.setdefault(file, [])
        # go build and go vet report a package's errors twice.
        if not any((e["line"], e["column"], e["message"]) == (error["line"], error["column"], error["message"]) for e in errors):
            errors.append(error)
        self.covered.add(file)

    def covers(self, file):
        return self.covers_all or os.path.normpath(file) in self.covered


def run(result, command, cwd, label=None):
    """Runs a command for a compiler result; returns (exit code, output), or (None, reason) when it could not start."""
    result.commands.append(f"(in {cwd}) " + (label or " ".join(command)))
    try:
        completed = subprocess.run(command, cwd=cwd, capture_output=True, text=True, errors="replace", timeout=COMMAND_TIMEOUT)
    except FileNotFoundError:
        return None, f"{command[0]} is not installed"
    except subprocess.TimeoutExpired:
        return None, f"{' '.join(command)} did not finish in {COMMAND_TIMEOUT // 60} minutes"
    return completed.returncode, completed.stdout + completed.stderr


def tail(text, lines=15):
    return "\n".join(text.strip().splitlines()[-lines:])


# --- Choosing the files -------------------------------------------------------------------------------------------

def is_test(relative):
    parts = pathlib.PurePosixPath(relative).parts
    name = parts[-1]
    return (any(p in TEST_FOLDERS for p in parts[:-1]) or name.endswith("_test.go") or name.startswith("test_")
            or name.endswith("_test.py") or re.search(r"\.(test|spec)\.[cm]?tsx?$", name) is not None)


def source_files(toolchain, folder):
    extensions = EXTENSIONS[toolchain]
    found = []
    for root, folders, files in os.walk(folder):
        folders[:] = sorted(f for f in folders if not f.startswith(".") and f not in SKIPPED_FOLDERS)
        for name in sorted(files):
            if name.endswith(extensions) and not name.endswith(".d.ts"):
                found.append(os.path.join(root, name))
    return found


def choose_files(toolchain, folder, limit=FILES_PER_REPOSITORY):
    """
    The files to open: from every folder in turn, shallowest first, so that the sample spans the repository rather
    than one package, with two of every three from the sources and one from the tests.
    """
    folder = os.path.abspath(folder)
    if toolchain not in EXTENSIONS or (toolchain == "node" and not typescript_configs(folder)):
        return []

    groups = {}
    for file in source_files(toolchain, folder):
        relative = os.path.relpath(file, folder).replace(os.sep, "/")
        groups.setdefault((is_test(relative), os.path.dirname(relative)), []).append(file)

    def round_robin(test):
        keys = sorted((k for k in groups if k[0] == test), key=lambda k: (k[1].count("/") + (k[1] != ""), k[1]))
        queues = [list(groups[k]) for k in keys]
        while any(queues):
            for queue in queues:
                if queue:
                    yield queue.pop(0)

    sources, tests = round_robin(False), round_robin(True)
    chosen = []
    while len(chosen) < limit:
        taken = 0
        for queue, count in ((sources, 2), (tests, 1)):
            for _ in range(count):
                file = next(queue, None)
                if file is not None and len(chosen) < limit:
                    chosen.append(file)
                    taken += 1
        if taken == 0:
            break
    return chosen


# --- TypeScript ---------------------------------------------------------------------------------------------------

def read_jsonc(path):
    """Reads a tsconfig.json: JSON with comments and trailing commas."""
    text = pathlib.Path(path).read_text(encoding="utf-8-sig")
    out, i, in_string = [], 0, False
    while i < len(text):
        c = text[i]
        if in_string:
            out.append(c)
            if c == "\\":
                out.append(text[i + 1:i + 2])
                i += 1
            elif c == '"':
                in_string = False
        elif c == '"':
            in_string = True
            out.append(c)
        elif text.startswith("//", i):
            while i < len(text) and text[i] != "\n":
                i += 1
            continue
        elif text.startswith("/*", i):
            end = text.find("*/", i + 2)
            i = len(text) if end < 0 else end + 2
            continue
        else:
            out.append(c)
        i += 1
    return json.loads(re.sub(r",(\s*[}\]])", r"\1", "".join(out)))


def typescript_configs(folder):
    """Every tsconfig.json under the folder, the files tsserver opens a project from."""
    configs = []
    for root, folders, files in os.walk(folder):
        folders[:] = sorted(f for f in folders if not f.startswith(".") and f not in SKIPPED_FOLDERS)
        if "tsconfig.json" in files:
            configs.append(os.path.join(root, "tsconfig.json"))
    return configs


def references(config):
    try:
        data = read_jsonc(config)
    except (OSError, ValueError):
        return []
    found = []
    for reference in data.get("references") or []:
        path = os.path.normpath(os.path.join(os.path.dirname(config), reference.get("path", "")))
        found.append(os.path.join(path, "tsconfig.json") if os.path.isdir(path) else path)
    return found


def included(config, file):
    """Roughly whether a tsconfig.json's "files" or "include" name a file: each pattern up to its first wildcard."""
    try:
        data = read_jsonc(config)
    except (OSError, ValueError):
        return False
    base = os.path.dirname(config)
    patterns = (data.get("files") or []) + (data.get("include") or ([] if "files" in data else ["**/*"]))
    for pattern in patterns:
        prefix = os.path.normpath(os.path.join(base, re.split(r"[*?{]", pattern, maxsplit=1)[0] or "."))
        if file == prefix or file.startswith(prefix.rstrip(os.sep) + os.sep):
            return True
    return False


def find_tsc(folder):
    for directory in [folder, *pathlib.Path(folder).parents]:
        candidate = os.path.join(directory, "node_modules", ".bin", "tsc")
        if os.path.exists(candidate):
            return candidate
    return shutil.which("tsc")


def typescript_programs(folder, run_tsc):
    """The program of every tsconfig.json and of the projects it references: {config: (files, errors by file)}."""
    programs = {}
    pending = typescript_configs(folder)
    while pending:
        config = pending.pop(0)
        if config in programs or not os.path.exists(config):
            continue
        programs[config] = run_tsc(config)
        pending.extend(references(config))
    return programs


def parse_tsc(output, cwd):
    files, errors = set(), {}
    for line in output.splitlines():
        match = TSC_ERROR.match(line)
        # TS6305 only says that a referenced project's output was not built, which tsc -b does first and tsserver,
        # reading the referenced project's sources, never needs.
        if match and match["code"] != "TS6305":
            file = os.path.normpath(os.path.join(cwd, match["file"]))
            errors.setdefault(file, []).append((int(match["line"]), int(match["column"]), match["code"], match["message"]))
        elif line.startswith("/") and line.strip().endswith((".ts", ".tsx", ".mts", ".cts", ".js", ".jsx", ".mjs", ".cjs", ".json")):
            files.add(os.path.normpath(line.strip()))
    return files, errors


def program_of(file, folder, programs):
    """The program tsserver puts a file in: the nearest tsconfig.json that includes it, or a project it references."""
    directory = os.path.dirname(file)
    while True:
        config = os.path.join(directory, "tsconfig.json")
        if config in programs:
            for candidate in [config, *references(config)]:
                if candidate in programs and file in programs[candidate][0]:
                    return candidate
        if os.path.normpath(directory) == os.path.normpath(folder) or os.path.dirname(directory) == directory:
            return None
        directory = os.path.dirname(directory)


def compile_typescript(folder, files):
    result = CompilerResult()
    tsc = find_tsc(folder)
    if tsc is None:
        result.problems.append("tsc is not installed and the project has none in node_modules")
        return result

    def run_tsc(config):
        code, output = run(result, [tsc, "--noEmit", "--pretty", "false", "--listFiles", "-p", config], folder)
        if code is None:
            result.problems.append(output)
            return set(), {}
        listed, errors = parse_tsc(output, folder)
        if not listed and not errors and code != 0:
            # A tsc without --listFiles: its program is then the files under the config's folder, unless it lists none.
            code, output = run(result, [tsc, "--noEmit", "--pretty", "false", "-p", config], folder)
            listed, errors = parse_tsc(output, folder)
            if code == 0 or errors:
                listed = {f for f in files if included(config, f)}
            else:
                result.problems.append(f"tsc -p {os.path.relpath(config, folder)} failed: {tail(output)}")
        return listed, errors

    programs = typescript_programs(folder, run_tsc)
    for file in files:
        config = program_of(file, folder, programs)
        if config is None:
            continue
        result.covered.add(file)
        for line, column, code, message in programs[config][1].get(file, []):
            result.add(file, line, column, code, message)
    return result


# --- Go -----------------------------------------------------------------------------------------------------------

def go_module(file, folder):
    directory = os.path.dirname(file)
    while True:
        if os.path.exists(os.path.join(directory, "go.mod")):
            return directory
        if os.path.normpath(directory) == os.path.normpath(folder) or os.path.dirname(directory) == directory:
            return None
        directory = os.path.dirname(directory)


def parse_go(output, cwd, result, vetting):
    """Adds go build's errors, or with vetting only go vet's type errors: its analyzers' findings are not errors."""
    for line in output.splitlines():
        match = GO_ERROR.match(line)
        if match and (line.startswith("vet: ") or not vetting):
            result.add(os.path.join(cwd, match["file"]), match["line"], match["column"], "go", match["message"])


def compile_go(folder, files):
    result = CompilerResult()
    for module in sorted({m for m in (go_module(f, folder) for f in files) if m is not None}):
        template = "{{.Dir}}|{{join .GoFiles \",\"}},{{join .CgoFiles \",\"}},{{join .TestGoFiles \",\"}},{{join .XTestGoFiles \",\"}}"
        code, output = run(result, ["go", "list", "-e", "-f", template, "./..."], module)
        if code is None:
            result.problems.append(output)
            return result
        for line in output.splitlines():
            if "|" in line:
                directory, names = line.split("|", 1)
                result.covered.update(os.path.normpath(os.path.join(directory, n)) for n in names.split(",") if n)

        for command, vetting in ((["go", "build", "./..."], False), (["go", "vet", "./..."], True)):
            before = sum(len(e) for e in result.errors.values())
            code, output = run(result, command, module)
            parse_go(output, module, result, vetting)
            found = sum(len(e) for e in result.errors.values()) - before
            # go vet also fails for its analyzers' findings; only a build that failed without errors is a problem.
            if code is None or (code != 0 and found == 0 and not vetting):
                result.problems.append(f"{' '.join(command)} in {os.path.relpath(module, folder) or '.'} failed: {tail(output)}")
    return result


# --- Rust ---------------------------------------------------------------------------------------------------------

def dep_info_files(target, folder):
    """The sources rustc read for each crate cargo checked, from its dep-info (.d) files."""
    sources = set()
    for root, _, names in os.walk(os.path.join(target, "debug")):
        for name in names:
            if not name.endswith(".d"):
                continue
            try:
                text = pathlib.Path(root, name).read_text(encoding="utf-8", errors="replace")
            except OSError:
                continue
            for token in re.split(r"(?<!\\)\s+", text.replace("\\\n", " ")):
                token = token.rstrip(":").replace("\\ ", " ")
                if token.endswith(".rs"):
                    path = os.path.normpath(token if os.path.isabs(token) else os.path.join(folder, token))
                    if path.startswith(os.path.normpath(folder) + os.sep):
                        sources.add(path)
    return sources


def parse_cargo(output, workspace, result):
    for line in output.splitlines():
        if not line.startswith("{"):
            continue
        try:
            message = json.loads(line)
        except ValueError:
            continue
        diagnostic = message.get("message") or {}
        if message.get("reason") != "compiler-message" or diagnostic.get("level") != "error":
            continue
        span = next((s for s in diagnostic.get("spans") or [] if s.get("is_primary")), None)
        if span is None:
            continue
        file = span["file_name"] if os.path.isabs(span["file_name"]) else os.path.join(workspace, span["file_name"])
        code = (diagnostic.get("code") or {}).get("code") or "rustc"
        result.add(file, span["line_start"], span["column_start"], code, diagnostic.get("message", ""))


def compile_rust(folder, files):
    result = CompilerResult()
    code, output = run(result, ["cargo", "metadata", "--format-version", "1", "--no-deps"], folder)
    if code != 0:
        result.problems.append(f"cargo metadata failed: {tail(output)}")
        return result
    metadata = json.loads(output[output.index("{"):].splitlines()[0])
    workspace, target = metadata["workspace_root"], metadata["target_directory"]
    code, output = run(result, ["cargo", "check", "--workspace", "--all-targets", "--keep-going", "--message-format=json"], workspace)
    if code is None:
        result.problems.append(output)
        return result
    parse_cargo(output, workspace, result)
    if code != 0 and not result.errors:
        result.problems.append(f"cargo check failed without compiler errors: {tail(output)}")
    result.covered.update(dep_info_files(target, folder))
    return result


# --- Java ---------------------------------------------------------------------------------------------------------

MAVEN_SKIPS = [
    "-DskipTests", "-Dcheckstyle.skip=true", "-Denforcer.skip=true", "-Dspring-javaformat.skip=true", "-Djacoco.skip=true",
    "-Dmaven.gitcommitid.skip=true", "-Dcyclonedx.skip=true", "-Dlicense.skip=true", "-Dspotless.check.skip=true",
    "-Dformatter.skip=true", "-Dpmd.skip=true", "-Dspotbugs.skip=true", "-Danimal.sniffer.skip=true",
]


def compile_java(folder, files):
    result = CompilerResult()
    if os.path.exists(os.path.join(folder, "pom.xml")):
        command, pattern = ["mvn", "-B", "test-compile", *MAVEN_SKIPS], MAVEN_ERROR
    elif any(os.path.exists(os.path.join(folder, f)) for f in ("build.gradle", "build.gradle.kts")):
        gradle = "./gradlew" if os.path.exists(os.path.join(folder, "gradlew")) else "gradle"
        command, pattern = [gradle, "--console=plain", "compileJava", "compileTestJava"], GRADLE_ERROR
    else:
        result.problems.append("neither pom.xml nor build.gradle: no Java build to compare with")
        return result

    code, output = run(result, command, folder)
    if code is None:
        result.problems.append(output)
        return result
    for line in output.splitlines():
        match = pattern.match(line.strip())
        if match:
            file = match["file"] if os.path.isabs(match["file"]) else os.path.join(folder, match["file"])
            result.add(file, match["line"], match.groupdict().get("column") or 1, "javac", match["message"])
    if code != 0 and not result.errors:
        result.problems.append(f"{' '.join(command[:3])} failed without compiler errors: {tail(output)}")
    result.covered.update(os.path.normpath(f) for f in files
                          if re.search(r"(^|/)src/(main|test)/java/", os.path.relpath(f, folder).replace(os.sep, "/")))
    return result


# --- Python -------------------------------------------------------------------------------------------------------

PYRIGHT_ROOT_MARKERS = ("pyproject.toml", "pyrightconfig.json", "setup.py", "setup.cfg", "requirements.txt", "Pipfile")


def virtual_environment(folder):
    for candidate in (".venv/bin/python", "venv/bin/python", ".venv/Scripts/python.exe"):
        path = os.path.join(folder, candidate)
        if os.path.lexists(path):
            return path
    return None


def pyright_root(file, folder):
    """The folder the IDE's Pyright serves a file from: the nearest with one of its root markers, as BuiltInLanguages.json lists them."""
    directory = os.path.dirname(file)
    while True:
        if any(os.path.exists(os.path.join(directory, m)) for m in PYRIGHT_ROOT_MARKERS):
            return directory
        if os.path.normpath(directory) == os.path.normpath(folder) or os.path.dirname(directory) == directory:
            return folder
        directory = os.path.dirname(directory)


def folders_up(start, folder):
    """A folder and the folders above it, up to the repository's."""
    directory = start
    while True:
        yield directory
        if os.path.normpath(directory) == os.path.normpath(folder) or os.path.dirname(directory) == directory:
            return
        directory = os.path.dirname(directory)


def configures_pyright(folder):
    if os.path.exists(os.path.join(folder, "pyrightconfig.json")):
        return True
    pyproject = os.path.join(folder, "pyproject.toml")
    if not os.path.exists(pyproject):
        return False
    text = pathlib.Path(pyproject).read_text(encoding="utf-8", errors="replace")
    if tomllib is not None:
        try:
            return "pyright" in (tomllib.loads(text).get("tool") or {})
        except tomllib.TOMLDecodeError:
            pass
    return re.search(r"^\[tool\.pyright\]", text, re.MULTILINE) is not None


def compile_python(folder, files):
    result = CompilerResult()
    result.covers_all = True
    interpreter = virtual_environment(folder) or shutil.which("python3") or sys.executable
    code, output = run(result, [interpreter, "-I", "-c", PY_COMPILE, *files], folder, f"{interpreter} -m py_compile <the {len(files)} files>")
    if code != 0:
        result.problems.append(f"py_compile could not run: {tail(output)}")
        return result
    for error in json.loads(output.strip().splitlines()[-1]):
        result.add(error["file"], error["line"], error["column"], error["code"], error["message"])
        result.whole_file.add(os.path.normpath(error["file"]))

    # Pyright, with the configuration of the folder the IDE's Pyright serves each file from, where it has one.
    roots = {}
    for file in files:
        roots.setdefault(pyright_root(file, folder), []).append(file)
    for root, checked in sorted(roots.items()):
        # Pyright looks for its configuration in the folder it serves and then above it, as click's examples, each with
        # a pyproject.toml of its own, get click's [tool.pyright].
        if not any(configures_pyright(d) for d in folders_up(root, folder)):
            continue
        environment = virtual_environment(root)
        command = ["pyright", "--outputjson", *(["--pythonpath", environment] if environment else []), *checked]
        code, output = run(result, command, root, " ".join(command[:-len(checked)]) + f" <the {len(checked)} files>")
        if code is None:
            result.problems.append("the project configures Pyright, but the pyright command is not installed")
            continue
        try:
            report = json.loads(output[output.index("{"):])
        except ValueError:
            result.problems.append(f"pyright did not report: {tail(output)}")
            continue
        for diagnostic in report.get("generalDiagnostics", []):
            if diagnostic.get("severity") == "error" and diagnostic.get("file"):
                start = diagnostic["range"]["start"]
                result.add(diagnostic["file"], start["line"] + 1, start["character"] + 1, diagnostic.get("rule") or "Pyright", diagnostic["message"].splitlines()[0])
    return result


COMPILERS = {"node": compile_typescript, "go": compile_go, "rust": compile_rust, "java": compile_java, "python": compile_python}


# --- Comparing ----------------------------------------------------------------------------------------------------

def compare(squiggles, compiler, folder):
    """Compares the editor's errors of each file with the compiler's: the result's counts and differences."""
    result = {"files": 0, "squiggles": 0, "compilerErrors": 0, "falseSquiggles": [], "missed": [], "notCompiled": [], "problems": list(compiler.problems)}
    for opened in squiggles.get("files", []):
        file = os.path.normpath(opened["path"])
        relative = os.path.relpath(file, folder).replace(os.sep, "/")
        if opened.get("state") != "Running":
            result["problems"].append(f"{relative}: {opened.get('server') or 'no language server'} is {opened.get('state') or 'not running'}, so it has no squiggles to compare")
            continue
        if not compiler.covers(file):
            result["notCompiled"].append({"file": relative, "squiggles": len(opened.get("errors", []))})
            continue

        result["files"] += 1
        editor = opened.get("errors", [])
        built = compiler.errors.get(file, [])
        result["squiggles"] += len(editor)
        result["compilerErrors"] += len(built)
        if file in compiler.whole_file:
            if not editor:
                result["missed"].extend({"file": relative, **e} for e in built)
            continue
        built_lines = {e["line"] for e in built}
        editor_lines = {e["line"] for e in editor}
        result["falseSquiggles"].extend({"file": relative, **e} for e in editor if e["line"] not in built_lines)
        result["missed"].extend({"file": relative, **e} for e in built if e["line"] not in editor_lines)
    return result


def describe(entry):
    code = f" {entry['code']}" if entry.get("code") else ""
    return f"{entry['file']}({entry['line']},{entry.get('column', 1)}):{code} {entry.get('message', '')}".strip()


def report_text(name, toolchain, result, commands):
    lines = [f"Squiggles of {name} ({LANGUAGE_NAMES[toolchain]}) next to the compiler's errors:"]
    lines += [f"  compiler: {c}" for c in commands]
    lines.append(f"  {result['files']} files compared: {result['squiggles']} editor errors, {result['compilerErrors']} compiler errors, "
                 f"{len(result['falseSquiggles'])} false squiggles, {len(result['missed'])} missed")
    lines += [f"  false squiggle: {describe(e)}" for e in result["falseSquiggles"][:60]]
    lines += [f"  missed: {describe(e)}" for e in result["missed"][:60]]
    lines += [f"  not compiled by the build, so not compared: {e['file']} ({e['squiggles']} editor errors)" for e in result["notCompiled"]]
    lines += [f"  PROBLEM: {p}" for p in result["problems"]]
    return "\n".join(lines)


def command_compare(arguments):
    folder = os.path.abspath(arguments.folder)
    squiggles = json.loads(pathlib.Path(arguments.squiggles).read_text(encoding="utf-8"))
    files = [os.path.normpath(f["path"]) for f in squiggles.get("files", [])]
    compiler = COMPILERS[arguments.toolchain](folder, files)
    result = compare(squiggles, compiler, folder)
    result.update({"name": arguments.name, "toolchain": arguments.toolchain, "commands": compiler.commands, "seconds": squiggles.get("seconds")})
    out = pathlib.Path(arguments.out, "squiggles")
    out.mkdir(parents=True, exist_ok=True)
    (out / f"{arguments.name}.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(report_text(arguments.name, arguments.toolchain, result, compiler.commands))
    return 0


def baselines():
    manifest = json.loads((pathlib.Path(__file__).parent / "real-repos.json").read_text(encoding="utf-8"))
    return manifest.get("squiggleBaselines") or {}


def summarize(results, limits):
    """The summary table and the problems that fail the job."""
    table = ["| Repository | Language | Files | Editor errors | Compiler errors | False squiggles | Missed | Baseline |",
             "| --- | --- | --- | --- | --- | --- | --- | --- |"]
    problems = []
    for result in sorted(results, key=lambda r: r["name"].lower()):
        name = result["name"]
        differences = len(result["falseSquiggles"]) + len(result["missed"])
        limit = limits.get(name, 0)
        table.append(f"| {name} | {LANGUAGE_NAMES.get(result['toolchain'], result['toolchain'])} | {result['files']} | {result['squiggles']} | "
                     f"{result['compilerErrors']} | {len(result['falseSquiggles'])} | {len(result['missed'])} | {limit} |")
        for problem in result["problems"]:
            problems.append(f"{name}: {problem}.")
        if differences > limit:
            problems.append(f"{name}: the editor's squiggles differ from the compiler's errors in {differences} places, more than its "
                            f"baseline of {limit} ({len(result['falseSquiggles'])} false, {len(result['missed'])} missed); see {name}-squiggles.txt.")
        elif differences < limit:
            problems.append(f"{name} improved to {differences} squiggle differences: lower its squiggleBaselines entry in "
                            f"build/ci/real-repos.json from {limit} to {differences}.")
    return "\n".join(table) + "\n", problems


def command_summary(arguments):
    folder = pathlib.Path(arguments.out, "squiggles")
    results = [json.loads(p.read_text(encoding="utf-8")) for p in sorted(folder.glob("*.json"))] if folder.exists() else []
    table, problems = summarize(results, baselines())
    pathlib.Path(arguments.out, "summary-squiggles.md").write_text(table, encoding="utf-8")
    print(table)
    for problem in problems:
        print(problem)
    return 1 if problems else 0


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n", 1)[0])
    commands = parser.add_subparsers(dest="command", required=True)
    files = commands.add_parser("files")
    files.add_argument("toolchain")
    files.add_argument("folder")
    files.add_argument("--limit", type=int, default=int(os.environ.get("TABSTUDIO_SQUIGGLE_FILES") or FILES_PER_REPOSITORY))
    comparing = commands.add_parser("compare")
    for option in ("--name", "--toolchain", "--folder", "--squiggles", "--out"):
        comparing.add_argument(option, required=True)
    summary = commands.add_parser("summary")
    summary.add_argument("--out", required=True)
    arguments = parser.parse_args()
    if arguments.command == "files":
        for file in choose_files(arguments.toolchain, arguments.folder, arguments.limit):
            print(file)
        return 0
    return command_compare(arguments) if arguments.command == "compare" else command_summary(arguments)


if __name__ == "__main__":
    sys.exit(main())
