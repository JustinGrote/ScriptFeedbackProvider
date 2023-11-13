using System.Management.Automation;
using static System.Management.Automation.Subsystem.SubsystemManager;
using static System.Management.Automation.Subsystem.SubsystemKind;
using static System.Management.Automation.Subsystem.SubsystemInfo;
using static System.Management.Automation.VerbsCommon;

namespace ScriptFeedbackProviderNS;

[Cmdlet(Get, "ScriptFeedbackProvider")]
public class GetScriptFeedbackProviderCommand : PSCmdlet
{
  protected override void EndProcessing()
  {
    IEnumerable<ImplementationInfo> implementations = GetSubsystemInfo(FeedbackProvider)
      .Implementations
      .Where(i => i.ImplementationType == typeof(ScriptFeedbackProvider));

    WriteObject(implementations, true);
  }
}
