using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using RNA_Rebuild_Admin.ServiceReference1;

namespace RNA_Rebuild_Admin
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	/// 
	public delegate bool SenderAdding(string server, int port, string address, string password, bool ssl, string reciever);
	public delegate bool LoginDelegate(string login, string password);
	public enum AdditionType
    {
		Image, Files, Process
    }
	public partial class MainWindow : Window, IService1Callback
	{
		private string Address { get; set; } = @"net.tcp://localhost:8523/Service";
		private RegistryKey addition = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\RNA_Addition\\");
		private Service1Client service;
		private Dictionary<string, bool> Back = null;
		private Client ActiveClient = null;
		public MainWindow()
		{
			Login lg = new Login();
			lg.OnLoginning += Lg_OnLoginning;
			lg.ShowDialog();

			InitializeComponent();
			service = new Service1Client(new System.ServiceModel.InstanceContext(this));
			service.AddAdminAsync(Dns.GetHostName(), System.Net.Dns.GetHostByName(Dns.GetHostName()).AddressList[0]);
			if (!service.CheckMailAsync().Result)
			{
				SetSMTP smtp = new SetSMTP();
				smtp.OnSenderAdd += Smtp_OnSenderAdd;
			}
			foreach (var client in service.GetClientsAsync().Result) Add_Client(client.Value);
		}

		private void DeleteAccount()
		{
			addition.DeleteValue("Password");
			addition.DeleteValue("Login");
		}

		private bool Smtp_OnSenderAdd(string server, int port, string address, string password, bool ssl, string reciever)
		{
			try
			{
				SmtpClient client = new SmtpClient(server, port);
				MailAddress adr = new MailAddress(address);
				adr = new MailAddress(reciever);

				Task.Run(()=>service.SetSMTPClientAsync(server, port, address, password, ssl, reciever));

				return true;
			}
			catch { return false; }
		}

		private bool Lg_OnLoginning(string Login, string Password)
		{
			string filename = System.IO.Path.GetTempFileName();
			try
			{
				File.WriteAllText(filename, Password);
				FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
				byte[] salt = Encoding.ASCII.GetBytes("RNASalt2021" + Login);
				Rfc2898DeriveBytes key = new Rfc2898DeriveBytes(Password, salt);
				byte[] bk = key.GetBytes(16);
				HMACSHA512 hash512 = new HMACSHA512(bk);
				hash512.ComputeHash(fs);
				fs.Close();
				byte[] pass = (byte[])addition.GetValue("Password");
				if (pass == null)
				{
					addition.SetValue("Password", hash512.Hash);
					addition.SetValue("Login", Login);
					return true;
				}
				else if (EqualHash(pass, hash512.Hash)
				&& Login == addition.GetValue("Login").ToString()) return true;
				else return false;
			}
			catch { return false; }
			finally {  File.Delete(filename); }
		}

		private bool EqualHash(byte[] b, byte[] b1)
		{
			if (b.Length != b1.Length) return false;
			for (int i = 0; i < b.Length; i++) if (b[i] != b1[i]) return false;
			return true;
		}

		private void Service_OnUserAdd(string pcname)
		{
			CheckBox chb = new CheckBox();
			chb.Content = pcname;
			chb.Checked += Chb_Checked;
			chb.Unchecked += Chb_Unchecked;
			this.ClientsPCListBox.Items.Add(chb);
		}

		private void Chb_Unchecked(object sender, RoutedEventArgs e)
		{
			try
			{
				service.DeleteUsingClientAsync(((CheckBox)sender).Content.ToString());
			}
			catch { }
		}

		private void Chb_Checked(object sender, RoutedEventArgs e)
		{
			try
			{
				service.AddUsingClientAsync(((CheckBox)sender).Content.ToString());
			}
			catch { }
		}

		Thread Stream = null;
		public async void GetScreenshot(Client Client)
		{
			Active.Content = Client.PcName;
			string filename = AppDomain.CurrentDomain.BaseDirectory + "\\Screenshots\\screenshot_" + Client.PcName + "_" + DateTime.Now + ".png";
			await Task.Run(() => File.WriteAllBytes(filename, service.GetScreenShotAsync(Client).Result.Content));
			BitmapImage bi = new BitmapImage(new Uri(filename, UriKind.RelativeOrAbsolute));
			Screen.Source = bi;
		}

		private void GoAnotherAdditionMode(AdditionType Type)
        {
			switch (Type)
            {
				case AdditionType.Files:
					ProcessList.Visibility = Visibility.Collapsed;
					Screen.Visibility = Visibility.Collapsed;
					FileBrowser.Visibility = Visibility.Visible;
					break;
				case AdditionType.Image:
					ProcessList.Visibility = Visibility.Collapsed;
					Screen.Visibility = Visibility.Visible;
					FileBrowser.Visibility = Visibility.Collapsed;
					break;
				case AdditionType.Process:
					ProcessList.Visibility = Visibility.Visible;
					Screen.Visibility = Visibility.Collapsed;
					FileBrowser.Visibility = Visibility.Collapsed;
					break;
			}
        }

		public async void GoStream(Client Client)
		{
			Active.Content = Client.PcName;
			Stream = new Thread(() => Streaming(Client));
			await Task.Run(() => Stream.Start());
		}

		public async void StopStream()
		{
			await Task.Run(() => Stream?.Abort());
			Stream = null;
		}

		public void Streaming(Client client)
		{
			while (true)
			{
				Thread.Sleep(20);
				File.WriteAllBytes(AppDomain.CurrentDomain.BaseDirectory + "\\Stream\\screen", service.GetScreenShot(client).Content);
				Thread tr = new Thread(() => ChangeStreamScreen());
				tr.Start();
			}
		}
		private void ChangeStreamScreen()
		{
			if (!Dispatcher.CheckAccess())
			{
				Dispatcher.Invoke(() => ChangeStreamScreen());
			}
			else
			{
				Screen.Source = new BitmapImage(new Uri(AppDomain.CurrentDomain.BaseDirectory + "\\Stream\\screen", UriKind.RelativeOrAbsolute));
			}
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem != null)
				GetScreenshot(service.GetClientsAsync().Result[((CheckBox)ClientsPCListBox.SelectedItem).Content.ToString()]);
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			service.ShutdownPCs(null);
		}

		private void ShutDownClientPC(object sender, RoutedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem != null)
				service.ShutdownPCsAsync(null);
		}
		private void RebootClientPC(object sender, RoutedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem != null)
				service.RebootPCsAsync(null);
		}
		private void DisconnectClientPC(object sender, RoutedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem != null)
				service.DisconnectClientAsync(null);
		}
		private void OpenFileBrowser(object sender, RoutedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem != null)
			{
				ActiveClient = service.GetClients()[((CheckBox)ClientsPCListBox.SelectedItem).Content.ToString()];
				Active.Content = ActiveClient.PcName; string[] drives = null;
				try
				{
					drives = service.GetClientDrivesAsync(ActiveClient).Result;
				}
				catch (Exception ex) { MessageBox.Show(ex.Message); }
				foreach (var drive in drives)
				{
					TreeViewItem new_item = new TreeViewItem();
					new_item.Header = drive;
					if (service.GetClientDirectories(ActiveClient, drive).Length > 0 || service.GetClientFiles(ActiveClient, drive).Length > 0)
						new_item.Items.Add(new TreeViewItem());
					FileBrowser.Items.Add(new_item);
				}
			}
		}
		private void New_item_Expanded(object sender, RoutedEventArgs e)
		{
			TreeViewItem tri = ((TreeViewItem)sender);
			tri.Items.Clear();
			List<DirectoryInfo> dirs = null;
			List<FileInfo> files = null;
			dirs = service.GetClientDirectoriesAsync(ActiveClient, tri.Header.ToString()).Result.ToList();
			files = service.GetClientFilesAsync(ActiveClient, tri.Header.ToString()).Result.ToList();
			foreach (var dir in dirs)
			{
				TreeViewItem new_item = new TreeViewItem();
				new_item.Tag = dir.FullName;
				new_item.Header = dir.Name;
				new_item.Expanded += New_item_Expanded;
				var d = service.GetClientDirectoriesAsync(ActiveClient, dir.FullName);
				if (service.GetClientDirectoriesAsync(ActiveClient, dir.FullName).Result.Length > 0 || ((IService1Callback)ActiveClient.Callback).GetFiles(dir.Name).Length > 0)
					new_item.Items.Add(new TreeViewItem());
			}
			foreach (var file in files)
			{
				TreeViewItem new_item = new TreeViewItem();
				new_item.Tag = file.FullName;
				new_item.Header = file.Name;
			}
		}

		private void Streaming_Start(object sender, RoutedEventArgs e)
		{
			GoAnotherAdditionMode(AdditionType.Image);
			if (ClientsPCListBox.SelectedItem != null && service.GetClientsAsync().Result.ContainsKey(((CheckBox)ClientsPCListBox.SelectedItem).Content.ToString())) GoStream(service.GetClients()[((CheckBox)ClientsPCListBox.SelectedItem).Content.ToString()]);
		}

		private void ClientsPCListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem == null) return;
			ActiveClient = service.GetClientsAsync().Result[((CheckBox)ClientsPCListBox.SelectedItem).Content.ToString()];
			Active.Content = ActiveClient.PcName;
		}
		private async void CheckBox_Checked(object sender, RoutedEventArgs e)
		{
			foreach (CheckBox box in ClientsPCListBox.Items)
			{
				await Task.Run(()=>Back.Add(box.Content.ToString(), (bool)box.IsChecked));
				if (box.IsChecked == false) box.IsChecked = true;
			}
		}

		private async void CheckBox_Unchecked(object sender, RoutedEventArgs e)
		{
			foreach (CheckBox box in ClientsPCListBox.Items)
				if (Back.ContainsKey(box.Content?.ToString()))
					await Task.Run(() => box.IsChecked = Back[box.Content.ToString()]);
		}

		public SuperFile GetScreenshot()
		{
			return null;
		}

		public void SendMessage(string mes)
		{
		}

		public void Reboot()
		{
		}

		public void ShutDown()
		{
		}

		public void Disconnect()
		{
		}

		public Process[] GetProcesses()
		{
			return null;
		}

		public void CloseProcess(int ProcessId)
		{
		}

		public DriveInfo[] GetDrives()
		{
			return null;
		}

		public FileInfo[] GetFiles(string path)
		{
			return null;
		}

		public DirectoryInfo[] GetDirectories(string path)
		{
			return null;
		}

		public bool RemoveFile(string path)
		{
			return false;
		}

		public string[] FindFiles(string mask)
		{
			return null;
		}

		public SuperFile TakeFile(string path)
		{
			return null;
		}

		public void Add_Client(Client cl)
		{
			if (!Dispatcher.CheckAccess())
			{
				Dispatcher.Invoke(() => Add_Client(cl));
			}
			else
			{
				if (cl == null) return;
				Service_OnUserAdd(cl.PcName);
			}
		}

		public void Remove_Client(Client cl)
		{
			for (int i = 0; i < ClientsPCListBox.Items.Count; i++)
			{
				if (((CheckBox)ClientsPCListBox.Items[i]).Content.ToString() == cl.PcName) { ClientsPCListBox.Items.RemoveAt(i); break; }
			}
		}

		public void UpdateClients(Dictionary<string, Client> UsingClients, Dictionary<string, Client> Clients)
		{

		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
		{
			try
			{
				service?.DeleteAdminAsync(Dns.GetHostName());
				Stream?.Abort();
				Stream = null;
			}
			catch { }
		}

        string[] IService1Callback.GetDrives()
        {
			return null;
        }

        private void OpenProcesses(object sender, RoutedEventArgs e)
        {
			var d = service.GetClientProcessesAsync(ActiveClient).Result;
			GoAnotherAdditionMode(AdditionType.Process);
			ProcessList.ItemsSource = d;
        }
    }
}