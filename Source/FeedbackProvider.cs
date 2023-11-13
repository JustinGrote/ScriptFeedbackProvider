using System.Threading.Tasks;
using static System.Management.Automation.Subsystem.SubsystemKind;
using static System.Management.Automation.Subsystem.SubsystemManager;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Management.Automation.Subsystem.Feedback;

namespace ScriptFeedbackProviderNS;

public class ScriptFeedbackProvider : IFeedbackProvider, IDisposable
{
  public ScriptBlock ScriptBlock { get; init; }
  public string Name { get; init; }
  public string Description { get; init; }
  public Guid Id { get; init; }
  public bool ShowDebugInfo { get; init; }
  private PowerShell Ps { get; set; }

  public ScriptFeedbackProvider
  (
    ScriptBlock scriptBlock,
    string? name,
    string? description,
    Guid? id,
    FeedbackTrigger? trigger,
    bool? showDebugInfo
  )
  {
    ScriptBlock = scriptBlock;
    Name = name ?? "Script Based Feedback";
    Description = description ?? "A feedback provider that runs a scriptblock";
    Id = id ?? Guid.NewGuid();
    Trigger = trigger ?? FeedbackTrigger.Error;
    ShowDebugInfo = showDebugInfo ?? false;
    Ps = PowerShell.Create();
  }

  #region IFeedbackProvider
  public FeedbackTrigger Trigger { get; }
  public FeedbackItem? GetFeedback(FeedbackContext context, CancellationToken token)
  {
    List<FeedbackItem>? results;

    try
    {
      // This should be uncommon but if our runspace is stuck due to an uncancelable script we need to swap it out for a fresh one and dispose the old one in the background.

      if (Ps.Runspace.RunspaceAvailability != RunspaceAvailability.Available)
      {
        if (ShowDebugInfo)
          Console.Error.WriteLine($"Script Feedback Provider {Name} ERROR: Runspace was still busy when we tried to resolve the next GetFeedback Request. This usually means your script is not cancelling correctly or fast enough.");

        var stuckPs = Ps;
        Ps = PowerShell.Create();
        Ps.Runspace = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2());
        Ps.Runspace.Open();

        _ = stuckPs.StopAsync(null, null);
      }

      var resultTask = Ps
        // Allows [FeedbackItem] to be used easily
        .AddScript("using namespace System.Management.Automation.Subsystem.Feedback")
        .AddScript(ScriptBlock.ToString())
        .AddArgument(context)
        .InvokeAsync();

      // The feedback provider silently times out at 300ms so we want to make this explicit.
      if (!resultTask.Wait(250))
      {
        if (ShowDebugInfo)
          Console.Error.WriteLine($"Script Feedback Provider {Name} ERROR: Script took longer than 250ms to execute. Feedback providers must complete in 300ms or less.");

        // Cancel the script
        _ = Ps.StopAsync(_ => { }, null);
        return null;
      }

      PSDataCollection<PSObject> resultItems = resultTask.GetAwaiter().GetResult();
      // TODO: Accept an array of strings, or a hashtable representation of feedbackitem
      results = resultItems
        .Select(x => x.BaseObject)
        .OfType<FeedbackItem>()
        .ToList();
    }
    catch (Exception err)
    {
      if (ShowDebugInfo)
        Console.Error.WriteLine($"Script Feedback Provider {Name} ERROR: {err}");

      return null;
    }
    finally
    {
      Ps.Commands.Clear();
    }

    if (results.Count < 1)
    {
      if (ShowDebugInfo)
        Console.Error.WriteLine($"Script Feedback Provider {Name} INFO: No feedback item was received");

      return null;
    }

    if (results.Count > 1 && ShowDebugInfo)
      Console.Error.WriteLine($"Script Feedback Provider {Name} WARN: Multiple feedback items were received, only the first will be used. This usually means your feedback provider was written incorrectly.");

    return results[0];
  }
  #endregion IFeedbackProvider

  public void Register()
  {
    RegisterSubsystem(FeedbackProvider, this);
    // Initialize and warm up the runspace to speed up later invocation
    Ps.Runspace = RunspaceFactory.CreateRunspace(InitialSessionState.CreateDefault2());
    Ps.Runspace.Open();
  }

  public void Dispose()
  {
    UnregisterSubsystem<ScriptFeedbackProvider>(Id);
  }
}
