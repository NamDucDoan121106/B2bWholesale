using B2BWholesale.WinForms.Forms;

namespace B2BWholesale.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        ApplicationConfiguration.Initialize();

        Application.ThreadException += (_, e) =>
            MessageBox.Show(Infrastructure.Ui.Friendly(e.Exception), "Unexpected error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        // Loop so "Log out" in the main window returns to the login screen.
        while (true)
        {
            using var login = new LoginForm();
            if (login.ShowDialog() != DialogResult.OK) break;

            using var main = new MainForm();
            Application.Run(main);
            if (!main.LoggedOut) break;
        }
    }
}
