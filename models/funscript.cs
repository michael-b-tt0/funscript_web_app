namespace funscript_web_app;

public class ActionData
{
    
    public int at { get; set; }
    
    public int pos { get; set; }
}

public class Metadata
{

    public string? script_url { get; set; }

    public string? title { get; set; }

}

public class Funscript
{
    public ActionData[]? actions { get; set; }
    public string? version { get; set; }
    public bool inverted { get; set; }
    public int range { get; set; }
    

    public Metadata? metadata { get; set; }

    public string? video_url { get; set; }

    public string? title { get; set; }

    List<List<ActionData>> actionGroups = new();

    public int GetactionGroupscount() => actionGroups.Count;




    public int firstActionAt
    {

        get
        {
            if (actions.Length > 0)
            {
                return actions[0].at;
            }
            else
            {
                return 0;
            }
        }
    }

    public int lastactionat
    {

        get
        {
            if (actions.Length > 0)
            {
                return actions[^1].at;
            }
            else
            {
                return 0;
            }
        }
    }


    public string duration
    {
        get
        {
            if (actions.Length > 0)
            {
                ActionData lastActionData = actions[actions.Length - 1];
                TimeSpan timeSpan = TimeSpan.FromMilliseconds(lastActionData.at);
                timeSpan = TimeSpan.FromMilliseconds(timeSpan.TotalMilliseconds + 500);
                DateTime referenceDateTime = new DateTime(1, 1, 1);
                DateTime durationDateTime = referenceDateTime.Add(timeSpan);
                return durationDateTime.ToString("HH:mm:ss");
            }
            else
            {
                return "00:00:00";
            }
        }
    }

    public int number_of_actions
    {
        get
        {
            // Check if the array is null to avoid NullReferenceException
            if (actions != null)
            {
                return actions.Length;
            }
            else
            {
                return 0; // or throw an exception, depending on desired behavior
            }
        }
    }

    public void generateActionGroups()
    {
        if (actions.Length > 0)
        {

            this.GetActionGroups(this.actions);
        }
    }

    public List<List<ActionData>> GetActionGroups(ActionData[] actions)
    {

        int index = -1;
        double timeSinceLast = -1;

        foreach (var action in actions)
        {
            if (Array.IndexOf(actions, action) == 0)
            {
                actionGroups.Add(new List<ActionData> { action });
                index++;
                continue;
            }

            if (Array.IndexOf(actions, action) == 1)
            {
                actionGroups[index].Add(action);
                timeSinceLast = Math.Max(250, (double)(action.at - actions[Array.IndexOf(actions, action) - 1].at));
                continue;
            }

            int newTimeSinceLast = (int)(action.at - actions[Array.IndexOf(actions, action) - 1].at);
            if (newTimeSinceLast > 5 * timeSinceLast)
            {
                actionGroups.Add(new List<ActionData> { action });
                index++;
            }
            else
            {
                actionGroups[index].Add(action);
            }

            timeSinceLast = Math.Max(250, newTimeSinceLast);
        }

        return actionGroups;
    }

    public static float GetSpeed(ActionData firstAction, ActionData secondAction)
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
    public static ActionData RoundAction(ActionData action)
    {
        ActionData outputAction = new ActionData
        {
            at = Math.Max(0, (int)Math.Round((float)action.at)),
            pos = Math.Max(0, Math.Min(100, (int)Math.Round((float)action.pos))),

        };

        return outputAction;
    }

    public int CalculateAverageSpeed(ActionData[] actions)
    {
        // Calculate the speed between each pair of consecutive actions and sum them up
        float totalSpeed = actions.Skip(1)
                                           .Select((action, index) => GetSpeed(actions[index], action))
                                           .Sum();

        // Calculate the average speed
        int averageSpeed = (int)(totalSpeed / (actions.Length - 1));

        return averageSpeed;
    }

    public int GetAverageSpeed() => CalculateAverageSpeed(actions);

    public ActionData[] FilterActionsBySpeed(float speedThreshold)
    {
        if (actions == null || actions.Length < 2)
            return actions ?? Array.Empty<ActionData>();

        List<ActionData> filteredActions = new List<ActionData>();
        filteredActions.Add(actions[0]); // Always include the first action

        for (int i = 1; i < actions.Length; i++)
        {
            float speed = GetSpeed(actions[i - 1], actions[i]);
            if (speed > speedThreshold)
            {
                filteredActions.Add(actions[i]);
            }
        }

        return filteredActions.ToArray();
    }


}

public class KeyAction : ActionData
{
    public string Type { get; set; }
    public List<ActionData> SubActions { get; set; } = new();
    public bool EligibleForHalving { get; set; }
}

