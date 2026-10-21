using System.Text.Json.Nodes;
namespace WinDiagnostics;
public static class Tool
{
    public const string Name="win-update-report";
    public static readonly string[] ValueOptions=["--days","--max-history"];
    public static readonly string[] Flags=[];
    public const string Help="win-update-report [--days 14] [--max-history 100] [--json]\nReads local Windows Update Agent history, relevant events, and reboot indicators. Does not scan for, download, or install updates.";
    public static void ValidateOptions(Options o){o.Number("--days",14,1,30);o.Number("--max-history",100,1,500);}
    public static Task<Collection> Collect(Options o,CancellationToken ct)=>PowerShell.Collect(new(){["WD_DAYS"]=o.Number("--days",14,1,30).ToString(),["WD_MAX"]=o.Number("--max-history",100,1,500).ToString()},ct);
    public static void ValidateEvidence(JsonObject e){J.Require(e,"history","events","rebootIndicators","historyCapped");foreach(var key in new[]{"history","events"})J.RequireRows(e,key);if(e["rebootIndicators"] is not JsonObject)throw new ArgumentException("Evidence rebootIndicators must be an object.");}
    public static string ExplainCode(string code)=>code.ToUpperInvariant() switch {"0X80070005"=>"Access was denied.","0X80070070"=>"Insufficient disk space was reported.","0X800F081F"=>"Required component source files could not be found.","0X80240022"=>"All updates in an operation failed; inspect individual results.","0X800F0922"=>"An installation failure was reported; this code alone does not identify its cause.",_=>"Consult the update's event and servicing logs for this HRESULT."};
    public static List<Finding> Analyze(JsonObject e)
    {
        var r=new List<Finding>();foreach(var h in J.Rows(e,"history"))
        {
            var code=(int)J.Number(h,"resultCode");if(code is 3 or 4 or 5)r.Add(new("update-operation-issue",code==4?"error":"warning",code switch {3=>"An update completed with errors",4=>"An update operation failed",_=>"An update operation was aborted"},$"{J.Text(h,"timeUtc")} | {J.Text(h,"title")} | {J.Text(h,"hresult")}: {ExplainCode(J.Text(h,"hresult"))} A past failure may have succeeded on a later retry.","Review later attempts and correlate with Windows Update and servicing events."));
        }
        var indicators=e["rebootIndicators"];
        if(J.Bool(indicators,"cbs")||J.Bool(indicators,"windowsUpdate"))r.Add(new("reboot-indicator","warning","A servicing reboot indicator is present","Component Based Servicing or Windows Update has a pending-reboot registry marker. Markers can be stale.","Plan a restart if appropriate, then collect again to verify the marker cleared."));
        if(J.Bool(indicators,"pendingFileRename"))r.Add(new("pending-file-operations","info","Pending file operations were recorded","PendingFileRenameOperations is nonempty; this can come from software unrelated to Windows Update.","Review the associated installation activity before attributing it to an update."));
        if(J.Bool(e,"historyCapped"))r.Add(new("history-capped","warning","Update history was capped","Only the newest requested history entries were read, then filtered to the time window.","Increase --max-history up to 500 for a fuller recent history."));
        if(J.Number(e,"systemDriveFreeBytes",double.MaxValue)<5*1073741824d)r.Add(new("low-system-disk","warning","System drive has less than 5 GiB free","Low space can interfere with updates; update space requirements vary.","Inspect disk usage before deleting or moving files."));return r;
    }
    public static IEnumerable<string> Describe(JsonObject e){yield return $"History entries: {J.Rows(e,"history").Count()} | service events: {J.Rows(e,"events").Count()}";foreach(var h in J.Rows(e,"history"))yield return $"{J.Text(h,"timeUtc")} | result {J.Text(h,"resultCode")} | {J.Text(h,"hresult")} | {J.Text(h,"title")}";yield return $"Reboot indicators: CBS={J.Text(e["rebootIndicators"],"cbs")} WindowsUpdate={J.Text(e["rebootIndicators"],"windowsUpdate")} pending file operations={J.Text(e["rebootIndicators"],"pendingFileRename")}";foreach(var row in J.Rows(e,"events"))yield return $"{J.Text(row,"timeUtc")} | {J.Text(row,"id")} | {J.Text(row,"message")}";}
    public static string[] SmokeArgs=>["--days","1","--max-history","5"];
}
