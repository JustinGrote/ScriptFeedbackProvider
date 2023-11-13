# PowerShell Script Based Feedback Providers

This module allows you to write feedback providers in PowerShell rather than C# and register them. A `[FeedbackContext]` is provided as an argument and you must return a single `[FeedbackItem]` object.

By default, feedback providers are registered for errors only. To register for another type, use the `-Trigger` parameter.

Example:

```powershell
Register-FeedbackProvider -Name EchoCommand -ScriptBlock {
  param($context)
  [FeedbackItem]::new("Command was", $context.CommandLine)
}
```

![Alt text](images/README/image.png)

## Authoring Intellisense

To get intellisense for $context, add the namespace at the top of your file.

```powershell
using namespace System.Management.Automation.Subsystem.Feedback
{
  param([FeedbackContext]$context)
  $context.<tab>
}
```

But do not include the namespace in your final command
