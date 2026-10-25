namespace TabStudio.Notebooks.Tests;

/// <summary>Real-world notebooks, exactly as their tools write them, including the final newline.</summary>
internal static class Fixtures
{
    /// <summary>A notebook saved by VS Code with Polyglot Notebooks: canonical Jupyter JSON.</summary>
    public const string VsCodeNotebook = """
        {
         "cells": [
          {
           "cell_type": "markdown",
           "metadata": {},
           "source": [
            "# Sales report\n",
            "\n",
            "Numbers for **October** — שלום 🙂"
           ]
          },
          {
           "cell_type": "code",
           "execution_count": 1,
           "id": "77476c72",
           "metadata": {
            "dotnet_interactive": {
             "language": "csharp"
            },
            "polyglot_notebook": {
             "kernelName": "csharp"
            },
            "vscode": {
             "languageId": "polyglot-notebook"
            }
           },
           "outputs": [
            {
             "name": "stdout",
             "output_type": "stream",
             "text": [
              "output\r\n"
             ]
            }
           ],
           "source": [
            "Console.Out.WriteLine(\"output\");"
           ]
          },
          {
           "cell_type": "code",
           "execution_count": 2,
           "id": "3a8f7a62",
           "metadata": {
            "dotnet_interactive": {
             "language": "csharp"
            },
            "polyglot_notebook": {
             "kernelName": "csharp"
            },
            "vscode": {
             "languageId": "polyglot-notebook"
            }
           },
           "outputs": [
            {
             "ename": "Error",
             "evalue": "System.Exception: oops!\r\n   at Submission#3.<<Initialize>>d__0.MoveNext()",
             "output_type": "error",
             "traceback": [
              "System.Exception: oops!\r\n",
              "   at Submission#3.<<Initialize>>d__0.MoveNext()"
             ]
            }
           ],
           "source": [
            "throw new Exception(\"oops!\");"
           ]
          },
          {
           "cell_type": "code",
           "execution_count": 3,
           "id": "82b70826",
           "metadata": {
            "dotnet_interactive": {
             "language": "csharp"
            },
            "polyglot_notebook": {
             "kernelName": "csharp"
            },
            "vscode": {
             "languageId": "polyglot-notebook"
            }
           },
           "outputs": [
            {
             "data": {
              "text/html": [
               "<table>\n",
               "<thead><tr><th>Name</th><th>Total</th></tr></thead>\n",
               "<tbody><tr><td>Ada &amp; Co</td><td>42</td></tr></tbody>\n",
               "</table>"
              ],
              "text/plain": [
               "Name       Total\n",
               "---------  -----\n",
               "Ada & Co   42"
              ]
             },
             "metadata": {},
             "output_type": "display_data"
            }
           ],
           "source": [
            "var sales = new[] { new { Name = \"Ada & Co\", Total = 42 } };\n",
            "display(sales);"
           ]
          },
          {
           "cell_type": "code",
           "execution_count": 4,
           "id": "1fd2d668",
           "metadata": {
            "dotnet_interactive": {
             "language": "fsharp"
            },
            "polyglot_notebook": {
             "kernelName": "fsharp"
            },
            "vscode": {
             "languageId": "polyglot-notebook"
            }
           },
           "outputs": [],
           "source": [
            "let answer = 42"
           ]
          },
          {
           "cell_type": "code",
           "execution_count": null,
           "id": "57e7624e",
           "metadata": {
            "dotnet_interactive": {
             "language": "csharp"
            },
            "polyglot_notebook": {
             "kernelName": "csharp"
            },
            "vscode": {
             "languageId": "polyglot-notebook"
            }
           },
           "outputs": [],
           "source": []
          }
         ],
         "metadata": {
          "kernelspec": {
           "display_name": ".NET (C#)",
           "language": "C#",
           "name": ".net-csharp"
          },
          "language_info": {
           "name": "C#"
          },
          "polyglot_notebook": {
           "kernelInfo": {
            "defaultKernelName": "csharp",
            "items": [
             {
              "aliases": [],
              "name": "csharp"
             },
             {
              "aliases": [],
              "languageName": "F#",
              "name": "fsharp"
             }
            ]
           }
          }
         },
         "nbformat": 4,
         "nbformat_minor": 5
        }
        """ + "\n";

    /// <summary>A notebook saved by Jupyter, with an attachment, a raw cell, JSON output and escapes.</summary>
    public const string JupyterNotebook = """
        {
         "cells": [
          {
           "attachments": {
            "chart.png": {
             "image/png": "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
            }
           },
           "cell_type": "markdown",
           "id": "e28c5c1b-ebd2-4b04-9a75-5c111646fc1f",
           "metadata": {
            "tags": [
             "intro"
            ]
           },
           "source": [
            "![chart](attachment:chart.png)"
           ]
          },
          {
           "cell_type": "code",
           "execution_count": 4,
           "id": "5fb03543-a4c3-4dd3-961d-fd3c6074c651",
           "metadata": {
            "collapsed": false,
            "custom_tool": {
             "ratio": 1.5,
             "seen": true
            }
           },
           "outputs": [
            {
             "data": {
              "application/json": {
               "answer": 42,
               "items": [
                1,
                2.25
               ]
              },
              "text/plain": [
               "A return value"
              ]
             },
             "execution_count": 4,
             "metadata": {},
             "output_type": "execute_result"
            },
            {
             "name": "stderr",
             "output_type": "stream",
             "text": [
              "warning: \"quoted\" \\ backslash\n",
              "tab\there \u001b[0m"
             ]
            }
           ],
           "source": [
            "\"A return value\""
           ]
          },
          {
           "cell_type": "raw",
           "id": "a1b2c3d4",
           "metadata": {
            "format": "text/x-rst"
           },
           "source": [
            ".. note:: raw text"
           ]
          }
         ],
         "metadata": {
          "kernelspec": {
           "display_name": ".NET (C#)",
           "language": "C#",
           "name": ".net-csharp"
          },
          "language_info": {
           "file_extension": ".cs",
           "mimetype": "text/x-csharp",
           "name": "C#",
           "pygments_lexer": "csharp",
           "version": "10.0"
          },
          "orig_nbformat": 4
         },
         "nbformat": 4,
         "nbformat_minor": 5
        }
        """ + "\n";

    /// <summary>An nbformat 4.2 notebook, from before cell ids.</summary>
    public const string OldJupyterNotebook = """
        {
         "cells": [
          {
           "cell_type": "code",
           "execution_count": null,
           "metadata": {},
           "outputs": [],
           "source": [
            "var x = 1;"
           ]
          }
         ],
         "metadata": {
          "kernelspec": {
           "display_name": ".NET (C#)",
           "language": "C#",
           "name": ".net-csharp"
          }
         },
         "nbformat": 4,
         "nbformat_minor": 2
        }
        """ + "\n";

    /// <summary>The .dib file dotnet/interactive's own tests round-trip, with five languages.</summary>
    public const string PolyglotDib = """
        #!meta

        {"kernelInfo":{"defaultKernelName":"csharp","items":[{"name":"csharp","aliases":["cs","C#","c#"]},{"name":"fsharp","aliases":["fs","F#","f#"]},{"name":"pwsh","aliases":["powershell"]},{"name":"mermaid"},{"name":"javascript"}]}}

        #!markdown

        * Markdown code

        #!csharp

        // C# code

        #!fsharp

        // F# code

        #!pwsh

        # PowerShell code

        #!javascript

        // JavaScript code

        #!mermaid

        %% Mermaid code
        """ + "\n";

    /// <summary>A C# .dib file with Markdown, a magic command and a kernel alias.</summary>
    public const string CSharpDib = """
        #!meta

        {"kernelInfo":{"defaultKernelName":"csharp","items":[{"aliases":["C#","c#"],"languageName":"C#","name":"csharp"},{"aliases":[],"name":"value"}]}}

        #!markdown

        # Prime numbers

        Using a **sieve**.

        #!csharp

        #!time
        static bool IsPrime(int n) => n > 1 && Enumerable.Range(2, (int)Math.Sqrt(n) - 1).All(d => n % d != 0);

        #!C#

        Enumerable.Range(1, 30).Where(IsPrime)
        """ + "\n";
}
