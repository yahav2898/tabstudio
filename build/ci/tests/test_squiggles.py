"""The squiggle comparison's own logic: choosing files, reading each compiler's errors, comparing and gating."""
import importlib.util
import json
import os
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("squiggles", Path(__file__).parents[1] / "squiggles.py")
squiggles = importlib.util.module_from_spec(spec)
spec.loader.exec_module(squiggles)


class SquiggleTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)

    def tearDown(self):
        self.temp.cleanup()

    def write(self, relative, text=""):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return str(path)

    def test_files_span_the_repository_with_a_third_from_the_tests(self):
        for name in ("a.go", "b.go", "a_test.go", "cmd/x/main.go", "internal/y/y.go", "vendor/z/z.go", "testdata/t.go", ".git/h.go"):
            self.write(name)

        chosen = [os.path.relpath(f, self.root) for f in squiggles.choose_files("go", self.root, limit=5)]

        # One file from every folder in turn, shallowest first; every third from the tests.
        self.assertEqual(["a.go", "cmd/x/main.go", "a_test.go", "internal/y/y.go", "b.go"], chosen)

    def test_a_node_repository_without_a_tsconfig_is_not_compared(self):
        self.write("index.ts")
        self.assertEqual([], squiggles.choose_files("node", self.root))
        self.write("tsconfig.json", "{}")
        self.write("types.d.ts")
        self.assertEqual([str(self.root / "index.ts")], squiggles.choose_files("node", self.root))

    def test_tsconfig_comments_and_trailing_commas_are_read(self):
        config = self.write("tsconfig.json", '{ // a solution\n "files": [], /* "x": 1 */ "references": [{ "path": "./app" },],\n "a": "http://x" }')
        self.write("app/tsconfig.json", "{}")
        self.assertEqual("http://x", squiggles.read_jsonc(config)["a"])
        self.assertEqual([str(self.root / "app" / "tsconfig.json")], squiggles.references(config))

    def test_a_file_is_compared_with_the_program_tsserver_gives_it(self):
        solution = self.write("tsconfig.json", '{ "files": [], "references": [{ "path": "./tsconfig.app.json" }] }')
        app = self.write("tsconfig.app.json", "{}")
        main = self.write("src/main.ts")
        output = f"src/main.ts(3,7): error TS2322: Type 'string' is not assignable to type 'number'.\n  continued\n{main}\n"
        programs = {solution: (set(), {}), app: squiggles.parse_tsc(output, str(self.root))}

        self.assertEqual(app, squiggles.program_of(main, str(self.root), programs))
        self.assertEqual([(3, 7, "TS2322", "Type 'string' is not assignable to type 'number'.")], programs[app][1][main])
        self.assertIsNone(squiggles.program_of(self.write("loose.ts"), str(self.root), programs))

    def test_without_listFiles_a_program_is_what_its_include_names(self):
        app = self.write("tsconfig.app.json", '{ "include": ["src/**/*.ts"] }')
        solution = self.write("tsconfig.json", '{ "files": [], "references": [] }')
        everything = self.write("other/tsconfig.json", "{}")
        main, loose = self.write("src/main.ts"), self.write("loose.ts")
        self.assertEqual((True, False), (squiggles.included(app, main), squiggles.included(app, loose)))
        self.assertFalse(squiggles.included(solution, main))
        self.assertTrue(squiggles.included(everything, self.write("other/deep/x.ts")))

    def test_go_vet_contributes_type_errors_but_not_its_analyzers_findings(self):
        result = squiggles.CompilerResult()
        squiggles.parse_go("# example.com/m\n./a.go:5:2: undefined: x\n", "/m", result, vetting=False)
        squiggles.parse_go("# example.com/m\nvet: ./a_test.go:7:9: undefined: y\n./b.go:3:2: fmt.Printf format %d has arg s of wrong type string\n", "/m", result, vetting=True)
        squiggles.parse_go("vet: ./a.go:5:2: undefined: x\n", "/m", result, vetting=True)

        self.assertEqual({"/m/a.go": [5], "/m/a_test.go": [7]}, {f: [e["line"] for e in errors] for f, errors in result.errors.items()})

    def test_cargo_errors_come_from_their_primary_span(self):
        message = {"reason": "compiler-message", "message": {"level": "error", "message": "mismatched types", "code": {"code": "E0308"},
                                                              "spans": [{"is_primary": False, "file_name": "src/x.rs", "line_start": 1, "column_start": 1},
                                                                        {"is_primary": True, "file_name": "src/lib.rs", "line_start": 2, "column_start": 21}]}}
        warning = {"reason": "compiler-message", "message": {"level": "warning", "message": "unused", "spans": [{"is_primary": True, "file_name": "src/lib.rs", "line_start": 9, "column_start": 1}]}}
        result = squiggles.CompilerResult()
        squiggles.parse_cargo("\n".join([json.dumps(message), json.dumps(warning), "error: could not compile `fx`"]), "/w", result)
        self.assertEqual({"/w/src/lib.rs": [{"line": 2, "column": 21, "code": "E0308", "message": "mismatched types"}]}, result.errors)

    def test_rustc_dep_info_names_the_files_it_read(self):
        lib = self.write("src/lib.rs")
        self.write("src/my module.rs")
        self.write("target/debug/deps/fx-1.d", f"{self.root}/target/debug/deps/libfx.rmeta: src/lib.rs src/my\\ module.rs\n\nsrc/lib.rs:\n")
        self.assertEqual({lib, str(self.root / "src" / "my module.rs")}, squiggles.dep_info_files(str(self.root / "target"), str(self.root)))

    def test_javac_errors_are_read_from_maven_and_gradle(self):
        maven = squiggles.MAVEN_ERROR.match("[ERROR] /w/src/main/java/App.java:[12,5] cannot find symbol")
        gradle = squiggles.GRADLE_ERROR.match("/w/src/main/java/App.java:12: error: cannot find symbol")
        self.assertEqual(("/w/src/main/java/App.java", "12", "5"), (maven["file"], maven["line"], maven["column"]))
        self.assertEqual(("/w/src/main/java/App.java", "12"), (gradle["file"], gradle["line"]))

    def test_py_compile_reports_syntax_errors_and_a_project_without_pyright_is_not_type_checked(self):
        good = self.write("good.py", "x: int = 'not checked without a type checker'\n")
        bad = self.write("bad.py", "def f(:\n    pass\n")

        result = squiggles.compile_python(str(self.root), [good, bad])

        self.assertEqual([], result.problems)
        self.assertEqual([bad], list(result.errors))
        self.assertEqual("SyntaxError", result.errors[bad][0]["code"])
        self.assertEqual({bad}, result.whole_file)
        self.assertEqual(1, len(result.commands))

    def test_pyright_counts_as_the_compiler_only_where_the_project_configures_it(self):
        self.write("pyproject.toml", "[project]\nname = 'x'\n[tool.mypy]\nstrict = true\n")
        self.assertFalse(squiggles.configures_pyright(str(self.root)))
        self.write("pyproject.toml", "[project]\nname = 'x'\n[tool.pyright]\ntypeCheckingMode = 'strict'\n")
        self.assertTrue(squiggles.configures_pyright(str(self.root)))

    def test_the_nearest_pyproject_is_the_configuration_pyright_loads(self):
        # click has [tool.pyright]; its examples each have a pyproject.toml without one, which Pyright loads instead.
        self.write("pyproject.toml", "[project]\nname = 'click'\n[tool.pyright]\ntypeCheckingMode = 'basic'\n")
        self.write("examples/aliases/pyproject.toml", "[project]\nname = 'aliases'\n")
        self.write("src/click/core.py", "x = 1\n")
        root = str(self.root)
        self.assertTrue(squiggles.pyright_configured_for(os.path.join(root, "src", "click"), root))
        self.assertFalse(squiggles.pyright_configured_for(os.path.join(root, "examples", "aliases"), root))

    def squiggles_of(self, *files):
        return {"files": [{"path": str(self.root / f), "server": "S", "state": state, "errors": [{"line": l, "column": 1, "code": "E", "message": "m"} for l in lines]}
                          for f, state, lines in files]}

    def test_errors_are_compared_per_file_and_line(self):
        compiler = squiggles.CompilerResult()
        compiler.add(str(self.root / "a.ts"), 3, 1, "TS1", "both")
        compiler.add(str(self.root / "a.ts"), 9, 1, "TS2", "only the compiler")
        compiler.covered.add(str(self.root / "b.ts"))

        result = squiggles.compare(self.squiggles_of(("a.ts", "Running", [3, 3, 5]), ("b.ts", "Running", []), ("c.ts", "Running", [1]), ("d.ts", "Failed", [])), compiler, str(self.root))

        self.assertEqual(2, result["files"])
        self.assertEqual([("a.ts", 5)], [(e["file"], e["line"]) for e in result["falseSquiggles"]])
        self.assertEqual([("a.ts", 9)], [(e["file"], e["line"]) for e in result["missed"]])
        self.assertEqual([{"file": "c.ts", "squiggles": 1}], result["notCompiled"])
        self.assertEqual(1, len(result["problems"]))
        self.assertIn("d.ts: S is Failed", result["problems"][0])

    def test_a_file_with_a_syntax_error_agrees_when_the_editor_shows_any_error(self):
        compiler = squiggles.CompilerResult()
        compiler.covers_all = True
        for name in ("a.py", "b.py"):
            compiler.add(str(self.root / name), 4, 1, "SyntaxError", "'(' was never closed")
            compiler.whole_file.add(str(self.root / name))

        result = squiggles.compare(self.squiggles_of(("a.py", "Running", [7, 8]), ("b.py", "Running", [])), compiler, str(self.root))

        self.assertEqual([], result["falseSquiggles"])
        self.assertEqual(["b.py"], [e["file"] for e in result["missed"]])

    def test_the_gate_holds_each_repository_to_its_baseline_and_asks_to_lower_it(self):
        def result(name, false, missed, problems=()):
            return {"name": name, "toolchain": "go", "files": 3, "squiggles": false, "compilerErrors": missed,
                    "falseSquiggles": [{}] * false, "missed": [{}] * missed, "notCompiled": [], "problems": list(problems)}

        table, problems = squiggles.summarize([result("clean", 0, 0), result("worse", 1, 1), result("better", 0, 1), result("broken", 0, 0, ["gopls is Failed"])],
                                              {"worse": 1, "better": 3})

        self.assertIn("| clean | Go | 3 | 0 | 0 | 0 | 0 | 0 |", table)
        self.assertEqual(3, len(problems))
        self.assertTrue(any(p.startswith("worse:") and "baseline of 1" in p for p in problems))
        self.assertTrue(any(p.startswith("better improved to 1") and "from 3 to 1" in p for p in problems))
        self.assertIn("broken: gopls is Failed.", problems)


if __name__ == "__main__":
    unittest.main()
