namespace RandySDR
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try
                {
                    var exText = e.ExceptionObject?.ToString() ?? "Unknown unhandled exception";
                    File.WriteAllText("startup-error.log", exText);
                }
                catch { }
            };

            try
            {
                // To customize application configuration such as set high DPI settings or default font,
                // see https://aka.ms/applicationconfiguration.
                ApplicationConfiguration.Initialize();
#pragma warning disable WFO5001
                Application.SetColorMode(SystemColorMode.Dark);
#pragma warning restore WFO5001
                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText("startup-error.log", ex.ToString());
                }
                catch { }

                MessageBox.Show(ex.ToString(), "Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }
    }
}