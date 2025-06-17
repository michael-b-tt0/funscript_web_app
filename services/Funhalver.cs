using funscript_web_app.models;

namespace funscript_web_app;



    public static class funscript_converter_funhalver_3
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
            float speed = lastAction != null ? Funscript.GetSpeed(lastAction, action) : 0;
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

    public static ActionData[] GenerateFinalActions(List<KeyAction> keyActions, Funscript funscipt, FunHalver_options_2 options)
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

                    if ( action.Type != "pause" && lastAction.SubActions.Any())
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

        public static Funscript GetHalfSpeedScript(Funscript funscript, FunHalver_options_2 options)
        {   // logger.LogWarning($"the funscript started of with {funscript.number_of_actions} actions", funscript);
            var filteredGroup = GetFilteredGroup(funscript);
        // logger.LogWarning($"after first filtering number left is{filteredGroup.Count}", filteredGroup.Count);
        var shortPauseRemovedGroup = RemoveShortPauses(filteredGroup, options);
        // logger.LogWarning($"after removing short pauses {shortPauseRemovedGroup.Count}", shortPauseRemovedGroup.Count);
            var keyActions = IdentifyKeyActions(shortPauseRemovedGroup, options);
        // logger.LogWarning($"number of keyactions over the speedlimit is {CountActionsExceedingThreshold(keyActions)}");
        // logger.LogWarning($"number of identified keyactions is {keyActions.Count}", keyActions.Count);
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

    public static int CountActionsExceedingThreshold(List<KeyAction> keyActions)
    {
        return keyActions.Count(action => action.EligibleForHalving);
    }

}



