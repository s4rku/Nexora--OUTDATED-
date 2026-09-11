using Microsoft.UI.Xaml;

namespace NEXORA;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            Application.Start((p) =>
            {
                var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        catch (Exception ex)
        {
            // Write crash to log before process dies
            var log = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NEXORA", "startup.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(log)!);
            System.IO.File.AppendAllText(log,
                $"{DateTime.Now:HH:mm:ss.fff}  PROGRAM.MAIN CRASH: {ex}{Environment.NewLine}");
        }
    }
}
