namespace AppsCm.Shell;

internal static class Program
{
  [STAThread]
  static void Main()
  {
    ApplicationConfiguration.Initialize();
    Application.Run(new ShellForm());
  }
}
