namespace BidiLint;

internal static class PlaceholderComparer
{
    public static PlaceholderComparison Compare(
        PlaceholderMessage baseMessage,
        PlaceholderMessage targetMessage)
    {
        return CompareShapes(
            PlaceholderShape.Build(baseMessage),
            PlaceholderShape.Build(targetMessage));
    }

    // Flattened plural branches are not one-to-one across cultures: a target
    // may have more categories, and a singular-like branch may legitimately
    // omit the count. Compare the union of placeholders across the group,
    // preserving the maximum multiplicity seen in any one branch so a branch
    // that genuinely repeats a placeholder is still checked.
    public static PlaceholderComparison ComparePluralGroup(
        IReadOnlyList<PlaceholderMessage> baseMessages,
        IReadOnlyList<PlaceholderMessage> targetMessages)
    {
        return CompareShapes(
            MergeShapes(baseMessages.Select(PlaceholderShape.Build)),
            MergeShapes(targetMessages.Select(PlaceholderShape.Build)));
    }

    private static PlaceholderComparison CompareShapes(
        PlaceholderShape baseShape,
        PlaceholderShape targetShape)
    {
        var nameDifference = CompareNames(
            baseShape.TopLevel,
            targetShape.TopLevel);
        if (nameDifference is not null)
        {
            return new PlaceholderComparison(
                PlaceholderDifferenceKind.Mismatch,
                Display(baseShape),
                Display(targetShape),
                nameDifference.Value.Base,
                nameDifference.Value.Target);
        }

        var formatDifference = CompareFormats(
            baseShape.TopLevel,
            targetShape.TopLevel);
        if (formatDifference is not null)
        {
            return new PlaceholderComparison(
                PlaceholderDifferenceKind.Type,
                Display(baseShape),
                Display(targetShape),
                formatDifference.Value.Base,
                formatDifference.Value.Target);
        }

        var targetGroups = targetShape.Groups
            .GroupBy(group => group.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var matchedTargetGroups = new HashSet<PlaceholderGroup>();

        foreach (var baseGroup in baseShape.Groups)
        {
            if (!targetGroups.TryGetValue(baseGroup.Name, out var candidates) ||
                candidates.FirstOrDefault(candidate => !matchedTargetGroups.Contains(candidate)) is not { } targetGroup)
            {
                return new PlaceholderComparison(
                    PlaceholderDifferenceKind.Mismatch,
                    Display(baseShape),
                    Display(targetShape),
                    baseGroup.Token,
                    null);
            }

            matchedTargetGroups.Add(targetGroup);

            if (baseGroup.Kind != targetGroup.Kind)
            {
                return new PlaceholderComparison(
                    PlaceholderDifferenceKind.Type,
                    Display(baseShape),
                    Display(targetShape),
                    baseGroup.Token,
                    targetGroup.Token);
            }

            if (baseGroup.Kind == PlaceholderArgumentKind.Select)
            {
                if (!baseGroup.Selectors.ToHashSet(StringComparer.Ordinal)
                        .SetEquals(targetGroup.Selectors))
                {
                    return new PlaceholderComparison(
                        PlaceholderDifferenceKind.Mismatch,
                        Display(baseShape),
                        Display(targetShape),
                        baseGroup.Token,
                        targetGroup.Token);
                }
            }
            else if (!targetGroup.Selectors.Contains("other", StringComparer.Ordinal))
            {
                return new PlaceholderComparison(
                    PlaceholderDifferenceKind.Mismatch,
                    Display(baseShape),
                    Display(targetShape),
                    baseGroup.Token,
                    targetGroup.Token);
            }

            var nestedNameDifference = CompareNames(
                baseGroup.NestedUses,
                targetGroup.NestedUses);
            if (nestedNameDifference is not null)
            {
                return new PlaceholderComparison(
                    PlaceholderDifferenceKind.Mismatch,
                    Display(baseShape),
                    Display(targetShape),
                    nestedNameDifference.Value.Base ?? baseGroup.Token,
                    nestedNameDifference.Value.Target ?? targetGroup.Token);
            }

            var nestedFormatDifference = CompareFormats(
                baseGroup.NestedUses,
                targetGroup.NestedUses);
            if (nestedFormatDifference is not null)
            {
                return new PlaceholderComparison(
                    PlaceholderDifferenceKind.Type,
                    Display(baseShape),
                    Display(targetShape),
                    nestedFormatDifference.Value.Base ?? baseGroup.Token,
                    nestedFormatDifference.Value.Target ?? targetGroup.Token);
            }
        }

        foreach (var targetGroup in targetShape.Groups)
        {
            if (matchedTargetGroups.Contains(targetGroup)) continue;
            if (baseShape.Groups.Any(group =>
                    string.Equals(group.Name, targetGroup.Name, StringComparison.Ordinal)))
            {
                continue;
            }

            return new PlaceholderComparison(
                PlaceholderDifferenceKind.Mismatch,
                Display(baseShape),
                Display(targetShape),
                null,
                targetGroup.Token);
        }

        return new PlaceholderComparison(
            PlaceholderDifferenceKind.None,
            Display(baseShape),
            Display(targetShape),
            null,
            null);
    }

    private static PlaceholderShape MergeShapes(IEnumerable<PlaceholderShape> shapes)
    {
        var shapeList = shapes.ToList();
        return new PlaceholderShape(
            MergeUses(shapeList.Select(shape => shape.TopLevel)),
            MergeGroups(shapeList.Select(shape => shape.Groups)));
    }

    private static List<PlaceholderUse> MergeUses(
        IEnumerable<IReadOnlyList<PlaceholderUse>> useLists)
    {
        var maxCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var representatives = new Dictionary<string, PlaceholderUse>(StringComparer.Ordinal);

        foreach (var uses in useLists)
        {
            var localCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var use in uses)
            {
                var key = UseSignature(use);
                localCounts[key] = localCounts.GetValueOrDefault(key) + 1;
                representatives.TryAdd(key, use);
            }

            foreach (var (key, count) in localCounts)
                maxCounts[key] = Math.Max(maxCounts.GetValueOrDefault(key), count);
        }

        var merged = new List<PlaceholderUse>();
        foreach (var (key, count) in maxCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var representative = representatives[key];
            for (var index = 0; index < count; index++)
                merged.Add(representative);
        }

        return merged;
    }

    private static List<PlaceholderGroup> MergeGroups(
        IEnumerable<IReadOnlyList<PlaceholderGroup>> groupLists)
    {
        var maxCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var representatives = new Dictionary<string, PlaceholderGroup>(StringComparer.Ordinal);
        var nestedUses = new Dictionary<string, List<IReadOnlyList<PlaceholderUse>>>(StringComparer.Ordinal);

        foreach (var groups in groupLists)
        {
            var localCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var key = GroupSignature(group);
                localCounts[key] = localCounts.GetValueOrDefault(key) + 1;
                representatives.TryAdd(key, group);
                if (!nestedUses.TryGetValue(key, out var nested))
                {
                    nested = new List<IReadOnlyList<PlaceholderUse>>();
                    nestedUses.Add(key, nested);
                }
                nested.Add(group.NestedUses);
            }

            foreach (var (key, count) in localCounts)
                maxCounts[key] = Math.Max(maxCounts.GetValueOrDefault(key), count);
        }

        var merged = new List<PlaceholderGroup>();
        foreach (var (key, count) in maxCounts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var representative = representatives[key];
            var mergedNested = MergeUses(nestedUses[key]);
            for (var index = 0; index < count; index++)
            {
                merged.Add(new PlaceholderGroup(
                    representative.Name,
                    representative.Kind,
                    representative.Selectors,
                    mergedNested,
                    representative.Token));
            }
        }

        return merged;
    }

    private static string UseSignature(PlaceholderUse use) =>
        use.Name + "\u0000" + (use.Format ?? string.Empty);

    private static string GroupSignature(PlaceholderGroup group) =>
        group.Name + "\u0000" +
        (int)group.Kind + "\u0000" +
        string.Join("\u0000", group.Selectors.Order(StringComparer.Ordinal));

    private static (PlaceholderToken? Base, PlaceholderToken? Target)? CompareNames(
        IReadOnlyList<PlaceholderUse> baseUses,
        IReadOnlyList<PlaceholderUse> targetUses)
    {
        var baseNames = baseUses.Select(use => use.Name).ToList();
        var targetNames = targetUses.Select(use => use.Name).ToList();
        if (baseNames.Count == targetNames.Count &&
            baseNames.Order(StringComparer.Ordinal)
                .SequenceEqual(targetNames.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return null;
        }

        var remainingTarget = targetNames.ToList();
        PlaceholderToken? missing = null;
        foreach (var use in baseUses)
        {
            var index = remainingTarget.IndexOf(use.Name);
            if (index >= 0)
            {
                remainingTarget.RemoveAt(index);
            }
            else
            {
                missing ??= use.Token;
            }
        }

        var remainingBase = baseNames.ToList();
        PlaceholderToken? extra = null;
        foreach (var use in targetUses)
        {
            var index = remainingBase.IndexOf(use.Name);
            if (index >= 0)
            {
                remainingBase.RemoveAt(index);
            }
            else
            {
                extra ??= use.Token;
            }
        }

        return (missing, extra);
    }

    private static (PlaceholderToken? Base, PlaceholderToken? Target)? CompareFormats(
        IReadOnlyList<PlaceholderUse> baseUses,
        IReadOnlyList<PlaceholderUse> targetUses)
    {
        var baseKeys = baseUses.Select(Key).ToList();
        var targetKeys = targetUses.Select(Key).ToList();
        if (baseKeys.Order(StringComparer.Ordinal)
            .SequenceEqual(targetKeys.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            return null;
        }

        foreach (var baseUse in baseUses)
        {
            var targetUse = targetUses.FirstOrDefault(use =>
                string.Equals(use.Name, baseUse.Name, StringComparison.Ordinal) &&
                !string.Equals(use.Format, baseUse.Format, StringComparison.Ordinal));
            if (targetUse is not null) return (baseUse.Token, targetUse.Token);
        }

        return (
            baseUses.Count > 0 ? baseUses[0].Token : null,
            targetUses.Count > 0 ? targetUses[0].Token : null);
    }

    private static string Key(PlaceholderUse use) =>
        use.Name + "\u0000" + (use.Format ?? string.Empty);

    private static string Display(PlaceholderShape shape)
    {
        var items = new List<string>();
        items.AddRange(shape.TopLevel.Select(use => use.Token.Display));
        items.AddRange(shape.Groups.Select(group =>
            group.Selectors.Count == 0
                ? group.Token.Display
                : $"{group.Name} ({group.Kind.ToString().ToLowerInvariant()}: {string.Join(", ", group.Selectors)})"));
        return string.Join(", ", items.Order(StringComparer.Ordinal));
    }

    private sealed record PlaceholderShape(
        IReadOnlyList<PlaceholderUse> TopLevel,
        IReadOnlyList<PlaceholderGroup> Groups)
    {
        public static PlaceholderShape Build(PlaceholderMessage message)
        {
            var topLevel = new List<PlaceholderUse>();
            var groups = new List<PlaceholderGroup>();
            Collect(message.Tokens, topLevel, groups);
            return new PlaceholderShape(topLevel, groups);
        }

        private static void Collect(
            IReadOnlyList<PlaceholderToken> tokens,
            List<PlaceholderUse> topLevel,
            List<PlaceholderGroup> groups)
        {
            foreach (var token in tokens)
            {
                if (token.Kind == PlaceholderTokenKind.IcuPound) continue;

                if (token.Kind == PlaceholderTokenKind.IcuArgument &&
                    token.ArgumentKind != PlaceholderArgumentKind.Simple)
                {
                    groups.Add(CreateGroup(token, groups));
                    continue;
                }

                topLevel.Add(new PlaceholderUse(token.Name, token.Format, token));
            }
        }

        private static PlaceholderGroup CreateGroup(
            PlaceholderToken token,
            List<PlaceholderGroup> groups)
        {
            var nested = new List<PlaceholderUse>();
            var branches = token.Branches ?? [];
            foreach (var branch in branches)
            {
                CollectNested(branch.Tokens, nested, groups);
            }

            return new PlaceholderGroup(
                token.Name,
                token.ArgumentKind,
                branches.Select(branch => branch.Selector).ToArray(),
                nested,
                token);
        }

        private static void CollectNested(
            IReadOnlyList<PlaceholderToken> tokens,
            List<PlaceholderUse> uses,
            List<PlaceholderGroup> groups)
        {
            foreach (var token in tokens)
            {
                if (token.Kind == PlaceholderTokenKind.IcuPound) continue;

                uses.Add(new PlaceholderUse(token.Name, token.Format, token));
                if (token.Kind == PlaceholderTokenKind.IcuArgument &&
                    token.ArgumentKind != PlaceholderArgumentKind.Simple)
                {
                    groups.Add(CreateGroup(token, groups));
                }
            }
        }
    }
}
