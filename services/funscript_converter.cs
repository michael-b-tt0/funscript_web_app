using funscript_web_app.Layout;
using funscript_web_app.models;
using Microsoft.Extensions.Options;

namespace funscript_web_app;

public static class funscript_converter_offset
{

    public static Funscript GetOffsetScript(Funscript funscript, int offset)
    {
        if (offset < -1 * funscript.lastactionat)
            return funscript;

        // Apply the offset to each action's timestamp
        ActionData[] modifiedActions = funscript.actions.Select(action =>
        {
            return new ActionData
            {
                at = action.at + offset,
                pos = action.pos
            };
        })
        // Filter out actions with negative timestamps
        .Where(action => action.at >= 0)
        // Round the action timestamps
        .Select(Funscript.RoundAction)
        .ToArray();

        // Create a new Funscript object with modified actions
        Funscript offsetScript = new Funscript
        {
            version = funscript.version,
            inverted = funscript.inverted,
            range = funscript.range,
            actions = modifiedActions,
            metadata = funscript.metadata,
            video_url = funscript.video_url,
            title = $"{funscript.title}_offset_{offset}"
        };

        return offsetScript;
    }

}

public static class funscript_converter_funhalver 
{ 
    public static List<ActionData> GetFilteredGroup(Funscript funscript)
    {
        return funscript.actions.Where((action, i) =>
        {
            // Always include the first and last actions
            if (i == 0 || i == funscript.number_of_actions - 1)
                return true;

            // Get the previous and next actions
            var lastAction = funscript.actions[i - 1];
            var nextAction = funscript.actions[i + 1];

            // Exclude actions that occur within a pause
            // (where current, previous, and next actions all have the same position)
            return !(action.pos == lastAction.pos && action.pos == nextAction.pos);
        }).ToList();
    }

    public static List<ActionData> RemoveShortPauses(List<ActionData> filteredGroup, FunHalver_options options)
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
                // Do nothing - we'll combine them at the next action
            }
            else
            {
                newFilteredGroup.Add(action);
            }
        }

        return newFilteredGroup;
    }

    public static List<KeyAction> IdentifyKeyActions(List<ActionData> filteredGroup, FunHalver_options options)
    {
        var keyActions = new List<KeyAction>();
        int apexCount = 0;

        for (int i = 0; i < filteredGroup.Count; i++)
        {
            var action = filteredGroup[i];
            if (i == 0)
            {
                keyActions.Add(new KeyAction { at = action.at, pos = action.pos, Type = "first" });
                continue;
            }
            if (i == filteredGroup.Count - 1)
            {
                keyActions.Add(new KeyAction { at = action.at, pos = action.pos, Type = "last" });
                continue;
            }

            var lastAction = filteredGroup[i - 1];
            var nextAction = filteredGroup[i + 1];

            if (action.pos == lastAction.pos)
            {
                keyActions.Add(new KeyAction { at = action.at, pos = action.pos, Type = "pause" });
                apexCount = 0;
            }
            else if (action.pos == nextAction.pos)
            {
                keyActions.Add(new KeyAction { at = action.at, pos = action.pos, Type = "prepause" });
                apexCount = 0;
            }
            else if (options.MatchFirstDownstroke && i == 1 && action.pos < lastAction.pos)
            {
                apexCount = 1;
            }
            else if (Math.Sign(action.pos - lastAction.pos) != Math.Sign(nextAction.pos - action.pos))
            {
                if (apexCount == 0)
                {
                    keyActions.Last().SubActions.Add(action);
                    apexCount++;
                }
                else
                {
                    keyActions.Add(new KeyAction { at = action.at, pos = action.pos, Type = "apex" });
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

    public static ActionData[] GenerateFinalActions(List<KeyAction> keyActions,Funscript funscipt, FunHalver_options options)
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
                    var max = Math.Max(lastAction.SubActions.Max(a => a.pos), action.pos);
                    var min = Math.Min(lastAction.SubActions.Min(a => a.pos), action.pos);
                    var newPos = Math.Abs(pos - min) > Math.Abs(pos - max) ? min : max;
                    outputAction.pos = newPos;
                    pos = newPos;
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

    public static Funscript GetHalfSpeedScript(Funscript funscript, FunHalver_options options)
    {
        var filteredGroup = GetFilteredGroup(funscript);
        var shortPauseRemovedGroup = RemoveShortPauses(filteredGroup, options);
        var keyActions = IdentifyKeyActions(shortPauseRemovedGroup, options);
        var finalActions = GenerateFinalActions(keyActions, funscript, options);

        Funscript HalfSpeedScript = new Funscript
        {
            version = funscript.version,
            inverted = funscript.inverted,
            range = funscript.range,
            actions = finalActions,
            metadata = funscript.metadata,
            video_url = funscript.video_url,
            title = $"{funscript.title}_HalfSpeed_with_pause_duration{options.ShortPauseDuration}"
        };

        return HalfSpeedScript;
    }
}

public static class funscript_converter_fundoubler
{

    public static List<Action> GetDoubleSpeedGroup(List<Action> actionGroup, FunDoublerOptions options)
    {
        var noShortPauses = RemoveShortPauses(actionGroup, options);
        var simplifiedGroup = SimplifyGroup(noShortPauses);
        return DoubleActions(simplifiedGroup, options);
    }

    private static ActionData[] RemoveShortPauses(ActionData[] actionGroup, FunDoublerOptions options)
    {
        return actionGroup.Where((action, i) =>
        {
            if (i == 0) return true;
            if (action.pos != actionGroup[i - 1].pos) return true;

            var diff = Math.Abs(action.at - actionGroup[i - 1].at);
            return diff > options.ShortPauseDuration;
        }).ToArray();
    }
    private static List<ActionData> SimplifyGroup(ActionData[] actions)
    {
        var simplifiedGroup = new List<ActionData>();
        for (int i = 0; i < actions.Length; i++)
        {
            if (i == 0 || i == actions.Length - 1)
            {
                simplifiedGroup.Add(actions[i]);
                continue;
            }
            int dirPrev = Math.Sign(actions[i].pos - actions[i - 1].pos);
            int dirNext = Math.Sign(actions[i + 1].pos - actions[i].pos);
            if (dirPrev != dirNext) simplifiedGroup.Add(actions[i]);
        }
        return simplifiedGroup;
    }

    private static List<ActionData> DoubleActions(List<ActionData> simplifiedGroup, FunDoublerOptions options)
    {
        var finalGroup = new List<ActionData>();
        int currentPos = simplifiedGroup[0].pos;

        for (int i = 0; i < simplifiedGroup.Count; i++)
        {
            var curAction = simplifiedGroup[i];

            if (i == simplifiedGroup.Count - 1)
            {
                if (!options.MatchGroupEnd) finalGroup.Add(curAction);
                continue;
            }

            var nextAction = simplifiedGroup[i + 1];

            int min = Math.Min(curAction.pos, nextAction.pos);
            int max = Math.Max(curAction.pos, nextAction.pos);
            int halfTime = curAction.at + (nextAction.at - curAction.at) / 2;
            int endTime = nextAction.at;
            bool minFurther = Math.Abs(currentPos - min) > Math.Abs(currentPos - max);

            if (i == 0)
            {
                finalGroup.Add(curAction);
                finalGroup.Add(new ActionData { at = halfTime, pos = minFurther ? min : max });
                finalGroup.Add(new ActionData { at = endTime, pos = minFurther ? max : min });
                currentPos = minFurther ? max : min;
                continue;
            }

            int dir = nextAction.pos - curAction.pos;
            if (dir == 0)
            {
                finalGroup.Add(new ActionData { at = nextAction.at, pos = currentPos });
                continue;
            }

            if (i == simplifiedGroup.Count - 2 && options.MatchGroupEnd)
            {
                finalGroup.Add(simplifiedGroup.Last());
                continue;
            }

            finalGroup.Add(new ActionData { at = halfTime, pos = minFurther ? min : max });
            finalGroup.Add(new ActionData { at = endTime, pos = minFurther ? max : min });
            currentPos = minFurther ? max : min;
            finalGroup.Add(new ActionData { at = endTime + 10, pos = currentPos });
        }

        return finalGroup;
    }

    
}
