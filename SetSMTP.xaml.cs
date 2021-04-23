using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace RNA_Rebuild_Admin
{
	/// <summary>
	/// Interaction logic for SetSMTP.xaml
	/// </summary>
	public partial class SetSMTP : Window
	{
		public Regex digits = new Regex("[^0-9]+");
		public event SenderAdding OnSenderAdd;
		public SetSMTP()
		{
			InitializeComponent();
		}
		private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
		{
			e.Handled = digits.IsMatch(e?.Text);
		}
		private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
		{
			TB_pass.Text = (sender as PasswordBox)?.Password;
		}
		private void CheckBox_Checked(object sender, RoutedEventArgs e)
		{
			Visibility vs = TB_pass.Visibility;
			TB_pass.Visibility = passbox.Visibility;
			passbox.Visibility = vs;
		}
		public bool IsValid(string emailaddress)
		{
			try
			{
				MailAddress m = new MailAddress(emailaddress);
				return true;
			}
			catch (FormatException)
			{
				return false;
			}
		}
		private void Add_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				string error = "Errors finded in next points:\n\n";

				if (TB_mail.Text?.Length == 0) error += "=> Mail address is missing!\n";
				else if (!IsValid(TB_mail.Text)) error += "=> Mail address isn't valid!\n";

				if (TB_pass.Text?.Length == 0) error += "=> Password is missing!\n";
				if (TB_server.Text?.Length == 0) error += "=> SMTP server address is missing!\n";

				if (TB_port.Text?.Length == 0) error += "=> SMTP server port is missing!\n";
				else if (Convert.ToInt32(TB_port.Text) > 65535) error += "=> SMTP port isn't valid!\n";

				if (TB_reciever.Text?.Length == 0) error += "=> Reciever mail address is missing!\n";
				else if (!IsValid(TB_reciever.Text)) error += "=> Reciever mail address isn't valid!\n";

				if (error.Length > 35) { MessageBox.Show(error); return; }

				if (OnSenderAdd.Invoke(TB_server.Text, Convert.ToInt32(TB_port.Text), TB_mail.Text, passbox.Password, (bool)SSL.IsChecked, TB_reciever.Text))
					this.Close();
				else MessageBox.Show("Something went wrong.");
			}
			catch (Exception ex) { MessageBox.Show(ex.Message); return; }
		}

		private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
		{

		}
	}
}
