using System.Globalization;
using System.Text;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>
/// Long source files of everyday application code for the scrolling benchmarks: C# that compiles against the base
/// library, with XML comments, regions, LINQ, async methods, switch expressions and interpolated strings, and Python with
/// type hints, dataclasses, comprehensions, f-strings and docstrings. A few lines carry the warnings real files carry,
/// so the editor draws squiggles. The same text is generated every time.
/// </summary>
internal static class CodeCorpus
{
    private static readonly string[] Domains =
    [
        "Inventory", "Shipping", "Billing", "Catalog", "Customer", "Payment", "Warehouse", "Supplier", "Pricing", "Returns",
        "Loyalty", "Invoice", "Delivery", "Order", "Account", "Report", "Schedule", "Ticket", "Booking", "Subscription",
        "Discount", "Refund", "Audit", "Review",
    ];

    /// <summary>A C# file of at least <paramref name="minimumLines"/> lines.</summary>
    /// <param name="minimumLines">The fewest lines the file has.</param>
    public static string CSharp(int minimumLines = 2400)
    {
        var text = new StringBuilder();
        text.Append("""
            using System;
            using System.Collections.Generic;
            using System.Globalization;
            using System.Linq;
            using System.Text;
            using System.Threading;
            using System.Threading.Tasks;

            namespace Greetings.Operations;

            /// <summary>Where an item is kept.</summary>
            public enum StorageZone
            {
                North,
                South,
                Cold,
                Hazardous,
                Overflow,
            }

            /// <summary>One line of stock: what it is, what it costs and how many there are.</summary>
            /// <param name="Sku">The stock keeping unit.</param>
            /// <param name="Name">The name customers see.</param>
            /// <param name="UnitPrice">The price of one.</param>
            /// <param name="Quantity">How many are in stock.</param>
            /// <param name="Zone">Where they are kept.</param>
            public sealed record StockLine(string Sku, string Name, decimal UnitPrice, int Quantity, StorageZone Zone)
            {
                /// <summary>The value of the whole line.</summary>
                public decimal Total => UnitPrice * Quantity;
            }


            """);
        for (var index = 0; Lines(text) < minimumLines; index++)
        {
            var domain = Domains[index % Domains.Length] + (index < Domains.Length ? string.Empty : (index / Domains.Length).ToString(CultureInfo.InvariantCulture));
            text.Append(CultureInfo.InvariantCulture, $"#region {domain}\n\n");
            text.Append(CSharpService(domain, index));
            text.Append(CSharpRules(domain, index));
            text.Append("#endregion\n\n");
        }

        return text.ToString();
    }

    /// <summary>A Python file of at least <paramref name="minimumLines"/> lines.</summary>
    /// <param name="minimumLines">The fewest lines the file has.</param>
    public static string Python(int minimumLines = 2200)
    {
        var text = new StringBuilder();
        text.Append(""""
            """Operations of a small online shop: stock, orders, invoices and reports."""

            from __future__ import annotations

            import asyncio
            import json
            import logging
            from collections import defaultdict
            from dataclasses import dataclass, field
            from datetime import datetime, timedelta
            from enum import Enum
            from typing import Iterable, Optional

            log = logging.getLogger(__name__)


            class Zone(Enum):
                """Where an item is kept."""

                NORTH = "north"
                SOUTH = "south"
                COLD = "cold"
                HAZARDOUS = "hazardous"


            @dataclass(frozen=True)
            class StockLine:
                """One line of stock."""

                sku: str
                name: str
                unit_price: float
                quantity: int
                zone: Zone = Zone.NORTH

                @property
                def total(self) -> float:
                    return self.unit_price * self.quantity



            """");
        for (var index = 0; Lines(text) < minimumLines; index++)
        {
            var domain = Domains[index % Domains.Length] + (index < Domains.Length ? string.Empty : (index / Domains.Length).ToString(CultureInfo.InvariantCulture));
            text.Append(PythonModule(domain, index)).Append('\n');
        }

        return text.ToString();
    }

    private static int Lines(StringBuilder text)
    {
        var lines = 0;
        foreach (var chunk in text.GetChunks())
        {
            lines += chunk.Span.Count('\n');
        }

        return lines;
    }

    private static string CSharpService(string domain, int index)
    {
        var noun = char.ToLowerInvariant(domain[0]) + domain[1..];
        var threshold = 5 + (index % 7);

        // Every third service keeps a variable it never reads, as real code does now and then: a warning squiggle.
        var leftover = index % 3 == 0 ? "\n        var unusedCount = 0;" : string.Empty;
        return $$"""
            /// <summary>
            /// Keeps the {{noun}} stock of one site: adds and removes lines, values them by zone, and reconciles them with
            /// a count. Thread-safe for reconciliation; everything else belongs to the caller's thread.
            /// </summary>
            public sealed class {{domain}}Service : IDisposable
            {
                private readonly Dictionary<string, StockLine> _lines = new(StringComparer.OrdinalIgnoreCase);
                private readonly List<string> _history = [];
                private readonly SemaphoreSlim _gate = new(1, 1);
                private int _version;

                /// <summary>Raised after a line was added or changed.</summary>
                public event EventHandler<StockLine>? Changed;

                /// <summary>The number of distinct lines.</summary>
                public int Count => _lines.Count;

                /// <summary>How many times the stock changed.</summary>
                public int Version => _version;

                /// <summary>Adds stock, merging it with a line of the same SKU.</summary>
                /// <param name="line">The stock to add.</param>
                public void Add(StockLine line)
                {
                    ArgumentNullException.ThrowIfNull(line);{{leftover}}
                    if (_lines.TryGetValue(line.Sku, out var existing))
                    {
                        _lines[line.Sku] = existing with { Quantity = existing.Quantity + line.Quantity };
                    }
                    else
                    {
                        _lines.Add(line.Sku, line);
                    }

                    _version++;
                    _history.Add($"{DateTime.UtcNow:O} added {line.Quantity} x {line.Name} ({line.Sku}) to {line.Zone}");
                    Changed?.Invoke(this, line);
                }

                /// <summary>The lines that are running low, fewest first.</summary>
                /// <param name="threshold">The quantity below which a line is low.</param>
                public IReadOnlyList<StockLine> LowStock(int threshold = {{threshold}}) =>
                    _lines.Values
                        .Where(l => l.Quantity < threshold && l.Zone != StorageZone.Hazardous)
                        .OrderBy(l => l.Quantity)
                        .ThenBy(l => l.Name, StringComparer.Ordinal)
                        .ToList();

                /// <summary>The value of the stock kept in a zone.</summary>
                /// <param name="zone">The zone.</param>
                public decimal ValueOf(StorageZone zone)
                {
                    decimal total = 0;
                    foreach (var line in _lines.Values)
                    {
                        if (line.Zone == zone)
                        {
                            total += line.Total;
                        }
                    }

                    return Math.Round(total, 2, MidpointRounding.AwayFromZero);
                }

                /// <summary>A sentence about a line for the {{noun}} report.</summary>
                /// <param name="line">The line.</param>
                public static string Describe(StockLine line) => line.Zone switch
                {
                    StorageZone.Cold => $"{line.Name} must stay below 4 degrees ({line.Quantity} left)",
                    StorageZone.Hazardous when line.Quantity > 100 => "Too much hazardous stock: " + line.Sku,
                    StorageZone.Hazardous => "Handle with care: " + line.Name,
                    StorageZone.Overflow => string.Format(CultureInfo.InvariantCulture, "{0} waits in overflow since {1:d}", line.Name, DateTime.Today),
                    _ => string.Format(CultureInfo.InvariantCulture, "{0} x {1} in {2}", line.Quantity, line.Name, line.Zone),
                };

                /// <summary>Replaces the lines whose counted quantity differs, and says how many changed.</summary>
                /// <param name="counted">The lines as counted.</param>
                /// <param name="cancellationToken">Stops the reconciliation.</param>
                public async Task<int> ReconcileAsync(IEnumerable<StockLine> counted, CancellationToken cancellationToken = default)
                {
                    ArgumentNullException.ThrowIfNull(counted);
                    await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        var changes = 0;
                        foreach (var line in counted)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!_lines.TryGetValue(line.Sku, out var known) || known.Quantity != line.Quantity)
                            {
                                _lines[line.Sku] = line;
                                changes++;
                            }
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds({{1 + (index % 4)}}), cancellationToken).ConfigureAwait(false);
                        return changes;
                    }
                    finally
                    {
                        _gate.Release();
                    }
                }

                /// <summary>Reads a line of a CSV export: SKU, name, price, quantity and zone.</summary>
                /// <param name="text">The CSV line.</param>
                /// <param name="line">The stock it describes.</param>
                public static bool TryParse(string text, out StockLine? line)
                {
                    line = null;
                    var parts = text.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length != 5
                        || !decimal.TryParse(parts[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var price)
                        || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var quantity)
                        || !Enum.TryParse<StorageZone>(parts[4], ignoreCase: true, out var zone))
                    {
                        return false;
                    }

                    line = new StockLine(parts[0], parts[1], price, quantity, zone);
                    return true;
                }

                /// <inheritdoc />
                public void Dispose() => _gate.Dispose();
            }


            """;
    }

    private static string CSharpRules(string domain, int index)
    {
        var limit = 10 * (index + 3);
        return $$"""
            /// <summary>The rules {{domain}} applies to an order before it is accepted.</summary>
            public static class {{domain}}Rules
            {
                private const int MaximumLines = {{limit}};
                private static readonly string[] BlockedPrefixes = ["TEST-", "TMP-", "X{{index}}-"];

                /// <summary>Checks an order and lists what is wrong with it.</summary>
                /// <param name="lines">The order's lines.</param>
                /// <returns>The problems; empty when the order is fine.</returns>
                public static IReadOnlyList<string> Validate(IReadOnlyCollection<StockLine> lines)
                {
                    var problems = new List<string>();
                    if (lines.Count == 0)
                    {
                        problems.Add("An order needs at least one line.");
                    }
                    else if (lines.Count > MaximumLines)
                    {
                        problems.Add($"An order has at most {MaximumLines} lines, not {lines.Count}.");
                    }

                    foreach (var line in lines)
                    {
                        if (BlockedPrefixes.Any(prefix => line.Sku.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        {
                            problems.Add($"{line.Sku} cannot be ordered.");
                        }

                        if (line.UnitPrice <= 0m || line.Quantity <= 0)
                        {
                            problems.Add($"{line.Sku}: price and quantity must be positive (got {line.UnitPrice:0.00} x {line.Quantity}).");
                        }
                    }

                    return problems;
                }

                /// <summary>The discount for an order's total, by tier.</summary>
                /// <param name="total">The order's total.</param>
                public static decimal DiscountFor(decimal total) => total switch
                {
                    >= 10_000m => total * 0.12m,
                    >= 1_000m => total * 0.05m,
                    >= 100m => 5m,
                    _ => 0m,
                };

                /// <summary>Groups lines by zone, with the value of each zone, most valuable first.</summary>
                /// <param name="lines">The lines.</param>
                public static IEnumerable<(StorageZone Zone, decimal Value, int Lines)> Summarize(IEnumerable<StockLine> lines) =>
                    from line in lines
                    group line by line.Zone into zone
                    let value = zone.Sum(l => l.Total)
                    orderby value descending
                    select (zone.Key, value, zone.Count());

                /// <summary>Writes a short plain-text report of the lines.</summary>
                /// <param name="lines">The lines.</param>
                public static string Report(IEnumerable<StockLine> lines)
                {
                    var builder = new StringBuilder();
                    builder.AppendLine(CultureInfo.InvariantCulture, $"{{domain}} report, {DateTime.Now:yyyy-MM-dd HH:mm}");
                    foreach (var (zone, value, count) in Summarize(lines))
                    {
                        // Columns line up for zones up to 12 characters.
                        builder.Append(zone.ToString().PadRight(12)).Append(' ')
                            .Append(value.ToString("N2", CultureInfo.InvariantCulture).PadLeft(14))
                            .Append(CultureInfo.InvariantCulture, $"  ({count} lines)")
                            .AppendLine();
                    }

                    return builder.ToString();
                }
            }


            """;
    }

    private static string PythonModule(string domain, int index)
    {
        var snake = domain.ToLowerInvariant();

        // Every fourth module calls a function that does not exist, as a file in progress does: an error squiggle.
        var typo = index % 4 == 0 ? $"        self._audit(f\"reconciled {{changes}} lines\")\n        summarize_{snake}(self)\n" : string.Empty;
        return $$""""
            class {{domain}}Service:
                """Keeps the {{snake}} stock of one site and reconciles it with a count."""

                low_threshold: int = {{5 + (index % 6)}}

                def __init__(self, site: str, lines: Optional[Iterable[StockLine]] = None) -> None:
                    self.site = site
                    self._lines: dict[str, StockLine] = {line.sku: line for line in lines or []}
                    self._history: list[str] = []
                    self._version = 0

                def __len__(self) -> int:
                    return len(self._lines)

                def __repr__(self) -> str:
                    return f"{{domain}}Service(site={self.site!r}, lines={len(self)})"

                def add(self, line: StockLine) -> None:
                    """Adds stock, merging it with a line of the same SKU."""
                    existing = self._lines.get(line.sku)
                    if existing is not None:
                        line = StockLine(line.sku, line.name, line.unit_price, existing.quantity + line.quantity, line.zone)
                    self._lines[line.sku] = line
                    self._version += 1
                    self._audit(f"added {line.quantity} x {line.name} ({line.sku}) to {line.zone.value}")

                def low_stock(self, threshold: Optional[int] = None) -> list[StockLine]:
                    """The lines running low, fewest first."""
                    limit = self.low_threshold if threshold is None else threshold
                    return sorted(
                        (line for line in self._lines.values() if line.quantity < limit and line.zone is not Zone.HAZARDOUS),
                        key=lambda line: (line.quantity, line.name),
                    )

                def value_of(self, zone: Zone) -> float:
                    total = 0.0
                    for line in self._lines.values():
                        if line.zone == zone:
                            total += line.total
                    return round(total, 2)

                def describe(self, line: StockLine) -> str:
                    match line.zone:
                        case Zone.COLD:
                            return f"{line.name} must stay below 4 degrees ({line.quantity} left)"
                        case Zone.HAZARDOUS if line.quantity > 100:
                            return "Too much hazardous stock: " + line.sku
                        case Zone.HAZARDOUS:
                            return "Handle with care: " + line.name
                        case _:
                            return "{} x {} in {}".format(line.quantity, line.name, line.zone.value)

                async def reconcile(self, counted: Iterable[StockLine], delay: float = 0.001) -> int:
                    """Replaces the lines whose counted quantity differs, and says how many changed."""
                    changes = 0
                    for line in counted:
                        known = self._lines.get(line.sku)
                        if known is None or known.quantity != line.quantity:
                            self._lines[line.sku] = line
                            changes += 1
                    await asyncio.sleep(delay)
            {{typo}}        return changes

                def summarize(self) -> dict[str, tuple[float, int]]:
                    groups: dict[str, list[StockLine]] = defaultdict(list)
                    for line in self._lines.values():
                        groups[line.zone.value].append(line)
                    return {zone: (round(sum(l.total for l in lines), 2), len(lines)) for zone, lines in groups.items()}

                def report(self) -> str:
                    rows = [f"{{domain}} report for {self.site}, {datetime.now():%Y-%m-%d %H:%M}"]
                    for zone, (value, count) in sorted(self.summarize().items(), key=lambda item: -item[1][0]):
                        rows.append(f"{zone:<12} {value:>14,.2f}  ({count} lines)")
                    return "\n".join(rows)

                def export(self) -> str:
                    return json.dumps([line.__dict__ for line in self._lines.values()], default=str, indent=2)

                def _audit(self, message: str) -> None:
                    stamp = datetime.utcnow().isoformat()
                    self._history.append(f"{stamp} {message}")
                    log.debug("%s: %s", self.site, message)


            def parse_{{snake}}_line(text: str) -> Optional[StockLine]:
                """Reads a line of a CSV export: SKU, name, price, quantity and zone."""
                parts = [part.strip() for part in text.split(",")]
                if len(parts) != 5:
                    return None
                try:
                    return StockLine(parts[0], parts[1], float(parts[2]), int(parts[3]), Zone(parts[4].lower()))
                except ValueError:
                    log.warning("Cannot read %r", text)
                    return None


            def {{snake}}_discount(total: float) -> float:
                if total >= 10_000:
                    return total * 0.12
                if total >= 1_000:
                    return total * 0.05
                return 5.0 if total >= 100 else 0.0


            def next_{{snake}}_count(last: datetime, every: timedelta = timedelta(days={{1 + (index % 9)}})) -> datetime:
                return last + every


            """";
    }
}
