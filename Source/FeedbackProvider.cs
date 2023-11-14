
using static System.Management.Automation.Subsystem.SubsystemKind;
using static System.Management.Automation.Subsystem.SubsystemManager;
using System.Collections;
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
    PSDataCollection<PSObject>? results;

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
        _ = Ps.StopAsync(null, null);
        return null;
      }

      results = resultTask.GetAwaiter().GetResult();
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

    return HandleFeedbackResult(results);
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

  private FeedbackItem? HandleFeedbackResult(PSDataCollection<PSObject> result)
  {
    if (result.Count < 1)
    {
      if (ShowDebugInfo)
        Console.Error.WriteLine($"Script Feedback Provider {Name} INFO: The script produced no output. This is normal if your feedback provider was not applicable/relevant");

      return null;
    }

    // If a feedbackItem was returned, return it
    var feedbackItemResult = result
      .Select(x => x.BaseObject)
      .OfType<FeedbackItem>()
      .ToList();

    if (feedbackItemResult.Count > 0)
    {
      if (ShowDebugInfo && feedbackItemResult.Count > 1)
        Console.Error.WriteLine($"Script Feedback Provider {Name} WARN: Multiple feedback items were received, only the first will be used. This usually means your feedback provider was written incorrectly.");

      return feedbackItemResult[0];
    }



    List<Hashtable> hashTableResult = result
      .Select(x => x.BaseObject)
      .OfType<Hashtable>()
      .ToList();

    if (hashTableResult.Count > 0)
    {
      if (ShowDebugInfo && hashTableResult.Count > 0)
        Console.Error.WriteLine($"Script Feedback Provider {Name} WARN: Multiple hashtables/dictionaries were received, only the first will be used. This usually means your feedback provider was written incorrectly, it shoud only return one hashtable/dictionary if this is the method you are using.");

      var dict = hashTableResult[0];

      if (!(dict.Contains("header") && dict["header"] is string header))
      {
        Console.Error.WriteLine($"Script Feedback Provider {Name} ERROR: Your script returned a hashtable/dictionary but it did not contain a 'header' key with a string value. This usually means your feedback provider was written incorrectly, your supplied hashtable/dictionary should contain a 'header' key with a string value.");
        return null;
      }

      if (!dict.Contains("actions"))
        return new FeedbackItem(header, null);

      if (dict["actions"] is string action)
      {
        return new FeedbackItem(header, [action]);
      }

      string[]? actionsArray = dict["actions"] as string[];

      if (actionsArray == null)
      {
        Console.Error.WriteLine($"Script Feedback Provider {Name} ERROR: Your script returned a hashtable/dictionary that had an 'actions' key with something other a string array value. This usually means your feedback provider was written incorrectly, your supplied hashtable/dictionary should contain an 'actions' key with a string array value.");
        return null;
      }

      return new FeedbackItem(header, actionsArray.ToList());
    }

    // If string(s) was returned, convert it to a feedback item
    var stringResult = result
      .Select(x => x.BaseObject)
      .OfType<string>()
      .ToList();

    if (stringResult.Count == 1)
    {
      return new FeedbackItem(stringResult[0], null);
    }
    if (stringResult.Count > 1)
    {
      string header = stringResult[0];
      stringResult.RemoveAt(0);
      return new FeedbackItem(header, stringResult);
    }

    Console.Error.WriteLine($"Script Feedback Provider {Name} ERROR: An unsupported object was output by your script. Only FeedbackProvider or string are supported.");
    return null;
  }
}
