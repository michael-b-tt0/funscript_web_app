using funscript_web_app.Layout;
using funscript_web_app.models;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
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

    public static Funscript GetHalfSpeedScript(Funscript funscript, FunHalver_options options, ILogger logger)
    {
        var filteredGroup = GetFilteredGroup(funscript);
        logger.LogWarning($"Filtered Group Count: {filteredGroup.Count}", filteredGroup);
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

    public static List<ActionData> GetDoubleSpeedGroup(List<ActionData> actionGroup, FunDoublerOptions options)
    {
        var noShortPauses = RemoveShortPauses(actionGroup, options);
        var simplifiedGroup = SimplifyGroup(noShortPauses);
        return DoubleActions(simplifiedGroup, options);
    }

    private static List<ActionData> RemoveShortPauses(List<ActionData> actionGroup , FunDoublerOptions options)
    {
        return actionGroup.Where((action, i) =>
        {
            if (i == 0) return true;
            if (action.pos != actionGroup[i - 1].pos) return true;

            var diff = Math.Abs(action.at - actionGroup[i - 1].at);
            return diff > options.ShortPauseDuration;
        }).ToList();
    }
    private static List<ActionData> SimplifyGroup(List<ActionData> actions)
    {
        var simplifiedGroup = new List<ActionData>();
        for (int i = 0; i < actions.Count; i++)
        {
            if (i == 0 || i == actions.Count - 1)
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
    
    public static Funscript GetDoubleSpeedScript(Funscript funscript, FunDoublerOptions options)
    {
        var output = new List<ActionData>();

        var orderedActions = funscript.actions.OrderBy(a => a.at).ToList();
        bool longFirstWait = orderedActions[1].at - orderedActions[0].at > 5000;

        if (longFirstWait) output.Add(orderedActions[0]);

        var actionGroups = longFirstWait
            ? GetActionGroups(orderedActions.Skip(1).ToList())
            : GetActionGroups(orderedActions);

        var fasterGroups = actionGroups.Select(group => GetDoubleSpeedGroup(group, options));

        foreach (var group in fasterGroups)
        {
            output.AddRange(group);
        }

        if (output.Last().at != funscript.actions.Last().at)
        {
            output.Add(new ActionData
            {
                at = funscript.actions.Last().at,
                pos = funscript.actions.Last().pos
            });
        }

        //output = output.Select(RoundAction).ToList();
        Funscript DoubledSpeedScript = new Funscript
        {
            version = funscript.version,
            inverted = funscript.inverted,
            range = funscript.range,
            actions = output.ToArray(),
            metadata = funscript.metadata,
            video_url = funscript.video_url,
            title = $"{funscript.title}_Doublespeed"
        };

        return DoubledSpeedScript;
    }

    public static List<List<ActionData>> GetActionGroups(List<ActionData> actions)
    {
        var actionGroups = new List<List<ActionData>>();
        int index = -1;
        int timeSinceLast = -1;

        for (int i = 0; i < actions.Count; i++)
        {
            if (i == 0)
            {
                actionGroups.Add(new List<ActionData> { actions[i] });
                index++;
                continue;
            }

            if (i == 1)
            {
                actionGroups[index].Add(actions[i]);
                timeSinceLast = Math.Max(250, actions[i].at - actions[i - 1].at);
                continue;
            }

            int newTimeSinceLast = actions[i].at - actions[i - 1].at;
            if (newTimeSinceLast > 5 * timeSinceLast)
            {
                actionGroups.Add(new List<ActionData> { actions[i] });
                index++;
            }
            else
            {
                actionGroups[index].Add(actions[i]);
            }

            timeSinceLast = Math.Max(250, newTimeSinceLast);
        }

        return actionGroups;
    }
}

public static class funscript_converter_funhalver_2
{
   

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
                // Swap actions
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
    public static List<ActionData> GetFilteredGroup(Funscript funscript, FunHalver_options_2 options)
    {
        var filteredActions = new List<ActionData>();
        filteredActions.Add(funscript.actions[0]); // Always include the first action

        for (int i = 1; i < funscript.actions.Length; i++)
        {
            var currentAction = funscript.actions[i];
            var lastAction = funscript.actions[i - 1];
            var nextAction = i < funscript.actions.Length - 1 ? funscript.actions[i + 1] : null;

            float speed = GetSpeed(lastAction, currentAction);

            // Include the action if:
            // 1. It's the last action, or
            // 2. Its speed is above the threshold, or
            // 3. It's not part of a pause (current, previous, and next actions don't all have the same position)
            if (i == funscript.actions.Length - 1 ||
                speed > options.SpeedThreshold ||
                !(currentAction.pos == lastAction.pos && (nextAction == null || currentAction.pos == nextAction.pos)))
            {
                filteredActions.Add(currentAction);
            }
        }

        return filteredActions;
    }

    public static List<ActionData> RemoveShortPauses(List<ActionData> filteredGroup, FunHalver_options_2 options)
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

    public static List<KeyAction> IdentifyKeyActions(List<ActionData> filteredGroup, FunHalver_options_2 options)
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
                if (apexCount == 0)
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
                keyActions.Last().SubActions.Add(action);
            }
        }

        return keyActions;
    }

    public static ActionData[] GenerateFinalActions(List<KeyAction> keyActions, Funscript funscript, FunHalver_options_2 options)
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

                if (action.Type != "pause" && lastAction.SubActions.Any() && action.EligibleForHalving)
                {
                    var max = Math.Max(lastAction.SubActions.Max(a => a.pos), action.pos);
                    var min = Math.Min(lastAction.SubActions.Min(a => a.pos), action.pos);
                    var newPos = Math.Abs(pos - min) > Math.Abs(pos - max) ? min : max;
                    outputAction.pos = newPos;
                    pos = newPos;
                }

                // If the action is eligible for halving, adjust its position
                if (action.Type != "pause" && lastAction.SubActions.Any() || !action.EligibleForHalving)
                {
                    /*outputAction.pos = action.pos;
                    pos = outputAction.pos;*/
                }
            }

            finalActions.Add(outputAction);
        }

        if (options.MatchGroupEndPosition && finalActions.Last().pos != funscript.actions.Last().pos)
        {
            var finalActionDuration = finalActions.Last().at - finalActions[finalActions.Count - 2].at;
            finalActions.Add(new ActionData
            {
                at = finalActions.Last().at + finalActionDuration,
                pos = funscript.actions.Last().pos
            });
        }

        return finalActions.Select(a => new ActionData
        {
            at = (int)Math.Floor(a.at + 0.5),
            pos = (int)Math.Floor(a.pos + 0.5)
        }).ToArray();
    }
    public static Funscript GetHalfSpeedScript(Funscript funscript, FunHalver_options_2 options, ILogger logger)
    {
        var filteredGroup = GetFilteredGroup(funscript, options);
        logger.LogWarning($"Filtered Group Count: {filteredGroup.Count}", filteredGroup);
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
            title = $"{funscript.title}_HalfSpeed_with_speed_threshold{options.SpeedThreshold}_pause_duration{options.ShortPauseDuration}"
        };

        return HalfSpeedScript;
    }

/*
    public static ActionData[] GenerateFinalActions(List<KeyAction> keyActions, Funscript funscript, FunHalver_options_2 options)
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
                    if (action.EligibleForHalving)
                    {
                        // Apply halving logic for eligible actions
                        var max = Math.Max(lastAction.SubActions.Max(a => a.pos), action.pos);
                        var min = Math.Min(lastAction.SubActions.Min(a => a.pos), action.pos);
                        var newPos = Math.Abs(pos - min) > Math.Abs(pos - max) ? min : max;
                        outputAction.pos = (newPos + lastAction.pos) / 2; // Halving
                        pos = outputAction.pos;
                    }
                    else
                    {
                        // For non-eligible actions, use the original position
                        outputAction.pos = action.pos;
                        pos = outputAction.pos;
                    }
                }
                else
                {
                    // For pauses or actions without SubActions, use the original position
                    outputAction.pos = action.pos;
                    pos = outputAction.pos;
                }
            }

            finalActions.Add(outputAction);
        }

        if (options.MatchGroupEndPosition && finalActions.Last().pos != funscript.actions.Last().pos)
        {
            var finalActionDuration = finalActions.Last().at - finalActions[finalActions.Count - 2].at;
            finalActions.Add(new ActionData
            {
                at = finalActions.Last().at + finalActionDuration,
                pos = funscript.actions.Last().pos
            });
        }

        return finalActions.Select(a => new ActionData
        {
            at = (int)Math.Floor(a.at + 0.5),
            pos = (int)Math.Floor(a.pos + 0.5)
        }).ToArray();
    }*/

}
