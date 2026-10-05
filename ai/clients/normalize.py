"""Normalise the .NET wire format into ordinary Python values.

## Why this exists

The .NET API serialises in a shape that is not what the agents want to read:

* **snake_case keys** -- ``pricing_type``, ``expected_days``, ``review_count``.
  Python convention, and what every agent in this codebase writes, is camelCase.
* **decimals as strings** -- ``"2500.00"``, ``"5.00"``, ``"650.00"``. Money and
  ratings arrive as JSON strings, deliberately: a float cannot represent 2500.005
  and silently rounding a payout is not acceptable. The cost is that every
  consumer has to remember to parse them.
* **enum names in PascalCase** -- ``"Fixed"`` rather than ``"fixed"``.

Done inline, that is a ``payload.get("pricing_type") or payload.get("pricingType")``
in a dozen places, and one of them will be wrong. Worse, the failure is silent:
a missing key yields ``None``, a ``None`` score yields ``0``, and a shortlist
comes back empty with no error anywhere.

So it is done once, here, at the boundary. Agents read camelCase keys and native
numbers, and a test asserts the two known quirks are handled -- so if the .NET
serialiser ever changes, exactly one test fails and the message says what moved.
"""

from __future__ import annotations

import re
from decimal import Decimal, InvalidOperation
from typing import Any

#: ``max_price`` -> ``maxPrice``. Handles the digits in ``s3_bucket`` style keys
#: correctly, because the split is on underscores rather than on case.
_SNAKE_BOUNDARY = re.compile(r"_([a-z0-9])")

#: Field names whose strings are decimal amounts, not text.
#:
#: Listed rather than inferred from "looks numeric", because a ``description``
#: that happens to be ``"1000"`` is prose and must stay a string. Inferring would
#: silently mangle free text.
DECIMAL_FIELDS = frozenset({
    "fixedPrice",
    "minPrice",
    "maxPrice",
    "priceUsd",
    # The price-band reference rows. `numeric(18,2)` arrives as a JSON string, and
    # an agent cannot compare or interpolate "4000.00" against anything. Added
    # with the price-bands endpoint rather than when the drift was first noticed,
    # because the list is explicit by design: nothing here is inferred, so a new
    # money field is invisible until it is named.
    "minPriceUsd",
    "maxPriceUsd",
    "budgetHint",
    "suggestedBudgetUsd",
    "averageRating",
    "commissionPercentage",
    "minBriefConfidence",
    "proposalScreeningThreshold",
    "matchScoreThreshold",
})

#: Fields that are counts or flags even though they arrive as strings.
_INT_FIELDS = frozenset({"reviewCount", "deliveryDays", "expectedDays"})


#: A leading run of capitals, e.g. the ``ID`` in ``IDNumber``.
_LEADING_CAPITALS = re.compile(r"^[A-Z]+(?![a-z])")


def to_camel(key: str) -> str:
    """One key into the camelCase the agents read.

    Handles both spellings .NET actually sends, because it sends both.

    The payloads that cross `DotNetClient` are snake_case (`min_price_usd`) and
    were the original reason this function existed. The categories injected on the
    request body are a different shape entirely: they are an anonymous projection
    (`new { c.Id, c.Name }`), which serialises as PascalCase (`Id`, `CategoryId`)
    and does not pass through this function's usual caller. A live draft hit the
    difference -- grounding looked for `"id"`, found nothing, and reported "Category
    id 1 is not a real category" about a category the request had just listed.

    So `IDNumber` becomes `idNumber` and not `iDNumber`: the whole leading capital
    run is lowered when it is an acronym, and only the first letter otherwise.
    """
    key = _SNAKE_BOUNDARY.sub(lambda m: m.group(1).upper(), key)
    if not key or not key[:1].isupper():
        return key
    run = _LEADING_CAPITALS.match(key)
    if run is not None:
        return run.group(0).lower() + key[run.end():]
    return key[0].lower() + key[1:]


def _coerce_decimal(value: Any) -> Any:
    """Parse a decimal string, leaving anything else alone.

    Returns a ``Decimal`` rather than a float. The score formula multiplies
    ratings and prices, and ``0.1 + 0.2 != 0.3`` in binary floating point is the
    kind of discrepancy that shows up as a commission a cent out.
    """
    if value is None or isinstance(value, (int, float, Decimal)):
        return value
    if not isinstance(value, str):
        return value
    try:
        return Decimal(value.strip())
    except (InvalidOperation, ValueError):
        # Not a number after all. Returned unchanged rather than coerced to 0,
        # because a silent 0 here would become a score of zero.
        return value


def normalise(value: Any) -> Any:
    """Recursively convert a .NET payload to camelCase keys and native numbers.

    Args:
        value: A decoded JSON value.

    Returns:
        The same structure with snake_case keys camelCased and known
        decimal/count fields parsed.
    """
    if isinstance(value, list):
        return [normalise(item) for item in value]

    if not isinstance(value, dict):
        return value

    out: dict[str, Any] = {}
    for raw_key, raw_value in value.items():
        key = to_camel(str(raw_key))
        converted = normalise(raw_value)

        if converted is None:
            # Nulls dropped. A missing key and an explicit null mean the same
            # thing to every agent here, and dropping them means a caller can use
            # `in` or `.get` without checking for both.
            continue

        if key in DECIMAL_FIELDS:
            converted = _coerce_decimal(converted)
        elif key in _INT_FIELDS and isinstance(converted, str):
            try:
                converted = int(converted)
            except ValueError:
                pass

        out[key] = converted

    return out
