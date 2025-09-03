namespace funscript_web_app;

public class FunQuarterOptions
{
    public bool RemoveShortPauses { get; set; }
    public int ShortPauseDuration { get; set; } = 2000;
    public bool MatchFirstDownstroke { get; set; }
    public bool ResetAfterPause { get; set; }
    public bool MatchGroupEndPosition { get; set; }
    public float SpeedThreshold { get; set; } = 0f;
    public int TopPercentile { get; set; } = 10;
}

public static class funscript_converter_funquarter
{
    public static List<ActionData> GetFilteredGroup(Funscript funscript)
    {
        return funscript.actions.Where((action, i) =>
        {
            if (i == 0 || i == funscript.number_of_actions - 1)
                return true;

            var lastAction = funscript.actions[i - 1];
            var nextAction = funscript.actions[i + 1];

            return !(action.pos == lastAction.pos && action.pos == nextAction.pos);
        }).ToList();
    }

    public static List<ActionData> RemoveShortPauses(List<ActionData> filteredGroup, FunQuarterOptions options)
    {
        if (!options.RemoveShortPauses) return filteredGroup;

        var newFilteredGroup = new List<ActionData>();
        int pauseTime = options.ShortPauseDuration;

        for (int i = 0; i < filteredGroup.Count; i++)
        {
            var action = filteredGroup[i];
            if (i == 0 || i == filteredGroup.Count - 1)
            {
                newFilteredGroup.Add(action);
                continue;
            }

            var lastAction = filteredGroup[i - 1];
            var nextAction = i < filteredGroup.Count - 1 ? filteredGroup[i + 1] : null;

            if (action.pos == lastAction.pos && Math.Abs(action.at - lastAction.at) < pauseTime)
            {
                newFilteredGroup.Add(new ActionData { at = (action.at + lastAction.at) / 2, pos = action.pos });
            }
            else if (nextAction != null && action.pos == nextAction.pos && Math.Abs(action.at - nextAction.at) < pauseTime)
            {
            }
            else
            {
                newFilteredGroup.Add(action);
            }
        }

        return newFilteredGroup;
    }

    public static List<KeyAction> IdentifyKeyActions(List<ActionData> filteredGroup, FunQuarterOptions options)
    {
        var keyActions = new List<KeyAction>();
        int apexCount = 0;

        for (int i = 0; i < filteredGroup.Count; i++)
        {
            var action = filteredGroup[i];
            var lastAction = i > 0 ? filteredGroup[i - 1] : null;
            var nextAction = i < filteredGroup.Count - 1 ? filteredGroup[i + 1] : null;
            float speed = lastAction != null ? GetSpeed(lastAction, action) : 0;
            bool isEligibleForHalving = speed > options.SpeedThreshold;
            KeyAction keyAction = new KeyAction
            {
                at = action.at,
                pos = action.pos,
                EligibleForHalving = isEligibleForHalving
            };
            if (i == 0)
            {
                keyAction.Type = "first";
                keyActions.Add(keyAction);
                continue;
            }
            if (i == filteredGroup.Count - 1)
            {
                keyAction.Type = "last";
                keyActions.Add(keyAction);
                continue;
            }

            if (action.pos == lastAction.pos)
            {
                keyAction.Type = "pause";
                keyActions.Add(keyAction);
                apexCount = 0;
            }
            else if (action.pos == nextAction.pos)
            {
                keyAction.Type = "prepause";
                keyActions.Add(keyAction);
                apexCount = 0;
            }
            else if (options.MatchFirstDownstroke && i == 1 && action.pos < lastAction.pos)
            {
                apexCount = 1;
            }
            else if (Math.Sign(action.pos - lastAction.pos) != Math.Sign(nextAction.pos - action.pos))
            {
                if (isEligibleForHalving)
                {
                    if (apexCount < 3)
                    {
                        keyActions.Last().SubActions.Add(action);
                        apexCount++;
                    }
                    else
                    {
                        keyAction.Type = "apex";
                        keyActions.Add(keyAction);
                        apexCount = 0;
                    }
                }
                else
                {
                    keyAction.Type = "apex";
                    keyActions.Add(keyAction);
                    apexCount = 0;
                }
            }
            else
            {
                keyActions.Last().SubActions.Add(action);
            }
        }

        return keyActions;
    }

    public static ActionData[] GenerateFinalActions(List<KeyAction> keyActions, Funscript funscipt, FunQuarterOptions options)
    {
        var finalActions = new List<ActionData>();
        int pos = options.ResetAfterPause ? 100 : keyActions[0].pos;

        for (int i = 0; i < keyActions.Count; i++)
        {
            var action = keyActions[i];
            ActionData outputAction;

            if (i == 0)
            {
                outputAction = new ActionData { at = action.at, pos = pos };
            }
            else
            {
                var lastAction = keyActions[i - 1];
                outputAction = new ActionData { at = action.at, pos = action.pos };

                if (action.Type != "pause" && lastAction.SubActions.Any())
                {
                    var newPos = (lastAction.SubActions.Sum(a => a.pos) + action.pos) / (lastAction.SubActions.Count + 1);
                    outputAction.pos = (int)Math.Round(newPos);
                    pos = outputAction.pos;
                }
            }

            finalActions.Add(outputAction);
        }

        if (options.MatchGroupEndPosition && finalActions.Last().pos != funscipt.actions.Last().pos)
        {
            var finalActionDuration = finalActions.Last().at - finalActions[finalActions.Count - 2].at;
            finalActions.Add(new ActionData
            {
                at = finalActions.Last().at + finalActionDuration,
                pos = funscipt.actions.Last().pos
            });
        }

        return finalActions.Select(a => new ActionData
        {
            at = (int)Math.Floor(a.at + 0.5),
            pos = (int)Math.Floor(a.pos + 0.5)
        }).ToArray();
    }

    public static Funscript GetQuarterSpeedScript(Funscript funscript, FunQuarterOptions options, ILogger logger)
    {
        logger.LogWarning($"the funscript started of with {funscript.number_of_actions} actions", funscript);
        var filteredGroup = GetFilteredGroup(funscript);
        logger.LogWarning($"after first filtering number left is{filteredGroup.Count}", filteredGroup.Count);

        if (options.TopPercentile > 0 && options.TopPercentile < 100)
        {
            var speeds = new List<float>();
            for (int i = 1; i < filteredGroup.Count; i++)
            {
                speeds.Add(GetSpeed(filteredGroup[i - 1], filteredGroup[i]));
            }
            speeds.Sort();
            int index = (int)Math.Floor(speeds.Count * (1 - options.TopPercentile / 100.0));
            if (index >= 0 && index < speeds.Count)
            {
                options.SpeedThreshold = speeds[index];
            }
        }
        else
        {
            options.SpeedThreshold = 0;
        }

        var shortPauseRemovedGroup = RemoveShortPauses(filteredGroup, options);
        logger.LogWarning($"after removing short pauses {shortPauseRemovedGroup.Count}", shortPauseRemovedGroup.Count);
        var keyActions = IdentifyKeyActions(shortPauseRemovedGroup, options);
        logger.LogWarning($"number of keyactions over the speedlimit is {CountActionsExceedingThreshold(keyActions)}");
        logger.LogWarning($"number of identified keyactions is {keyActions.Count}", keyActions.Count);
        var finalActions = GenerateFinalActions(keyActions, funscript, options);

        Funscript QuarterSpeedScript = new Funscript
        {
            version = funscript.version,
            inverted = funscript.inverted,
            range = funscript.range,
            actions = finalActions,
            metadata = funscript.metadata,
            video_url = funscript.video_url,
            title = $"{funscript.title}_QuarterSpeed_with_pause_duration{options.ShortPauseDuration}"
        };

        return QuarterSpeedScript;
    }

    public static int CountActionsExceedingThreshold(List<KeyAction> keyActions)
    {
        return keyActions.Count(action => action.EligibleForHalving);
    }

    private static float GetSpeed(ActionData firstAction, ActionData secondAction)
    {
        if (firstAction == null || secondAction == null)
            return 0;
        if (firstAction.at == secondAction.at)
            return 0;
        try
        {
            if (secondAction.at < firstAction.at)
            {
                var temp = secondAction;
                secondAction = firstAction;
                firstAction = temp;
            }
            return 1000f * ((float)Math.Abs(secondAction.pos - firstAction.pos) / (float)Math.Abs(secondAction.at - firstAction.at));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed on actions {firstAction}, {secondAction}: {ex.Message}");
            return 0;
        }
    }
}
