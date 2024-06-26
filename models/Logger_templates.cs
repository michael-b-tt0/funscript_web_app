using funscript_web_app.Layout;

namespace funscript_web_app.models;




    public static partial class LoggerExtensions
{
    [LoggerMessage(5001, LogLevel.Information, "log_html_cavas_render log_number: {lognumber} current action at: {current_action_at} {current_action_Pos} {prev_Action_at} {prev_Action_Pos} {x_value} {y_value} {speed} {HexValue}")]
    public static partial void Log_html_cavas_render(this ILogger logger,int lognumber, int current_action_at, int current_action_Pos, int prev_Action_at, int prev_Action_Pos, float x_value, float y_value, float speed, string HexValue);

    [LoggerMessage(103, LogLevel.Warning, "Number of funscript actions is {Length}")]
    public static partial void Log_number_of_funscript_actions(this ILogger logger, int Length);


    

}
