

namespace funscript_web_app;


public static class Logfiletodownload
{

    public static List<Log_csv_entry> Logsfile = new();
}
public class Log_csv_entry
{

    public double current_action_at { get; set; }

    public double current_action_Pos { get; set; }
    public double prev_Action_at
    {
        get; set;

    }
    public double prev_Action_Pos { get; set; }
    public double x_value {  get; set; }
    public double y_value { get; set; }

    public double speed { get; set; }

    



}