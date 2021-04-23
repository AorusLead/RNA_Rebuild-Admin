using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
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
		private string ExePath = System.Windows.Forms.Application.ExecutablePath;
		private RegistryKey addition = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\RNA_Addition\\");
		private RegistryKey reg = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run\\");
		private Service1Client service = null;
		private Dictionary<string, bool> Back = new Dictionary<string, bool>();
		private Client ActiveClient = null;
		private Thread Stream = null;
		private DispatcherTimer ProcessWatchingTimer = new DispatcherTimer();
		private DispatcherTimer ProtectionTimer = new DispatcherTimer();
		public MainWindow()
		{
			if (!Directory.Exists(AppDomain.CurrentDomain.BaseDirectory + "Screenshots")) Directory.CreateDirectory(AppDomain.CurrentDomain.BaseDirectory + "Screenshots");
			InitializeComponent();
		}

		private void GettingUsers()
		{
			foreach (var client in service.GetClientsAsync().Result) Add_Client(client.Value);
		}

		private void CheckingMail()
		{
			if (!service.CheckMailAsync().Result)
			{
				SetSMTP smtp = new SetSMTP();
				smtp.OnSenderAdd += Smtp_OnSenderAdd;
				smtp.ShowDialog();
			}
		}

		private void CheckingReg()
		{
			try 
			{
				bool value = reg.GetValue("RNA_Admin") != null;
				if (value) { startupitem.IsChecked = Convert.ToBoolean(value); MenuItem_Click_1(null, null); }

				{
					value = false;
					bool.TryParse(addition.GetValue("MailLogs")?.ToString(), out value);
					if (value) { sendmaillogsitem.IsChecked = Convert.ToBoolean(value); sendmaillogsitem_Click(null, null); }
				}
				{
					value = false;
					bool.TryParse(addition.GetValue("TextLogs")?.ToString(), out value);
					if (value) { textlogsitem.IsChecked = Convert.ToBoolean(value); MenuItem_Click_3(null, null); }
				}
				{
					value = false;
					bool.TryParse(addition.GetValue("SafeMode")?.ToString(), out value);
					if (value) { safemodeitem.IsChecked = Convert.ToBoolean(value); MenuItem_Click_2(null, null);  }
				}
			}
			catch (Exception ex) { MessageBox.Show(ex.Message); }
		}

		[Obsolete]
		private void ConnectingToService()
		{
			do
			{
				try
				{
					service = new Service1Client(new System.ServiceModel.InstanceContext(this));
					service.AddAdminAsync(Dns.GetHostName(), System.Net.Dns.GetHostByName(Dns.GetHostName()).AddressList[0]);
				}
				catch (FaultException) { MessageBox.Show("Looks like service was faulted."); }
				catch (CommunicationException) { MessageBox.Show("Looks like service is offline or faulted."); }
				catch { MessageBox.Show("Looks like service isn't online."); }
			}
			while (service == null);
		}

		private void SetTimers()
		{
			ProcessWatchingTimer.Tick += Timer_Tick;
			ProtectionTimer.Tick += ProtectionTimer_Tick;

			ProcessWatchingTimer.Interval = new TimeSpan(0, 0, 4);
			ProtectionTimer.Interval = new TimeSpan(0, 0, 5);
		}

		private void Loginning()
		{
			Login lg = new Login();
			lg.OnLoginning += Lg_OnLoginning;
			lg.ShowDialog();
		}

		private void ProtectionTimer_Tick(object sender, EventArgs e)
		{
			ProtectionTimer.Stop();
			Loginning();
			ProtectionTimer.Start();
		}

		private void Timer_Tick(object sender, EventArgs e)
		{
			OpenProcesses(null, null);
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
				if ((bool)SelectAllCheckBox.IsChecked) SelectAllCheckBox.IsChecked = false;
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

		public void GetScreenshot(Client Client)
		{
			GoAnotherAdditionMode(AdditionType.Image);
			Active.Content = Client.PcName;
			string filename = (AppDomain.CurrentDomain.BaseDirectory + "Screenshots\\screenshot_" + Client.PcName + "_" + Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory + "Screenshots").Length + ".png");
			SuperImage ScreenShot = service.GetScreenShotAsync(Client.PcName).Result;
			File.WriteAllBytes(filename, ScreenShot.Content);
			Dispatcher.Invoke(()=> Screen.Source = new BitmapImage(new Uri(filename, UriKind.RelativeOrAbsolute)));
		}

		private void GoAnotherAdditionMode(AdditionType Type)
        {
			if (!Dispatcher.CheckAccess()) Dispatcher.Invoke(() => GoAnotherAdditionMode(Type));
			else
			switch (Type)
            {
				case AdditionType.Files:
					ProcessWatchingTimer?.Stop();
					ProcessList.Visibility = Visibility.Collapsed;
					Screen.Visibility = Visibility.Collapsed;
					FileBrowser.Visibility = Visibility.Visible;
					break;
				case AdditionType.Image:
					ProcessWatchingTimer?.Stop();
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
			StopStream();
			Active.Content = Client.PcName;
			Stream = new Thread(() => Streaming(Client));
			await Task.Run(() => Stream.Start());
		}

		public async void StopStream()
		{
			await Task.Run(() => Stream?.Abort());
			Stream = null;
		}

		public delegate void ChinimaHuinima(BitmapImage bm);

		public void Streaming(Client client)
		{
			//MessageBox.Show(service.State.ToString());
			while (true)
			{
				try
				{
					SuperImage si = service.GetScreenShotAsync(client.PcName).Result;

					using (var ms = new System.IO.MemoryStream(si.Content))
					{
						var image = new BitmapImage();
						image.BeginInit();
						image.CacheOption = BitmapCacheOption.OnLoad;
						image.StreamSource = ms;
						image.EndInit();
						new Thread(()=>Task.Factory.StartNew(()=>ChangeScreen(image))).Start();
					}
				}
				catch (FaultException ex)
				{
					MessageBox.Show(ex.Code.SubCode.Name + "\n" + ex.Code.Name);
					MessageBox.Show(ex.Reason.ToString());
					MessageBox.Show(ex.Message);
				}
				catch (Exception ex)
				{
					MessageBox.Show(ex.Message);
				}
			}
		}

		

		private void ChangeStreamScreen(BitmapImage bm)
		{
			//if (!Dispatcher.CheckAccess())
			//{
			//	//new ChinimaHuinima(ChangeStreamScreen).BeginInvoke(bm, null, null);
			//	//Dispatcher.Invoke(() => ChangeStreamScreen(bm));
			//	Dispatcher.BeginInvoke(new ChinimaHuinima(ChangeStreamScreen), bm);
			//}
			//else
			//{
			//	Screen.Source = bm; //Image
			//		//((Bitmap)new ImageConverter().ConvertFrom(arr));
			//}
			if (!Screen.CheckAccess())
				Screen.Dispatcher.Invoke(() => Screen.Source = bm);

			//if (!Screen.Dispatcher.CheckAccess())
			//Screen.Dispatcher.Invoke(() => Screen.Source = bm);
		}
		void ChangeScreen(BitmapImage bm)
		{
			if (!Screen.CheckAccess())
			{
				Screen.Dispatcher.Invoke(new Action<BitmapImage>(ChangeScreen), bm);
			}
			else
			{
				Screen.Source = bm;
			}
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem != null)
				GetScreenshot(service.GetClientsAsync().Result[((CheckBox)ClientsPCListBox.SelectedItem).Content.ToString()]);
		}

		private void Button_Click(object sender, RoutedEventArgs e)
		{
			service.ShutdownPCsAsync(null);
		}

		private void ShutDownClientPC(object sender, RoutedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem != null)
				service.ShutdownPCsAsync(ActiveClient.PcName);
		}
		private void RebootClientPC(object sender, RoutedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem != null)
				service.RebootPCsAsync(ActiveClient.PcName);
		}
		private void DisconnectClientPC(object sender, RoutedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem != null)
				service.DisconnectClientAsync(ActiveClient.PcName);
		}
		private void OpenFileBrowser(object sender, RoutedEventArgs e)
		{
			if (ClientsPCListBox.SelectedItem != null)
			{
				GoAnotherAdditionMode(AdditionType.Files);
				Active.Content = ActiveClient.PcName; 
				SuperDrive[] drives = null;
				try
				{
					drives = service.GetClientDrivesAsync(ActiveClient.PcName).Result;
					foreach (var drive in drives)
					{
						TreeViewItem new_item = new TreeViewItem();
						new_item.Tag = new_item.Header = drive.Name;
						if (!drive.IsEmpty)
							new_item.Items.Add(new TreeViewItem());
						new_item.Expanded += New_item_Expanded;
						FileBrowser.Items.Add(new_item);
					}
				}
				catch (Exception ex) { MessageBox.Show(ex.Message); }
			}
		}
		private void New_item_Expanded(object sender, RoutedEventArgs e)
		{
			TreeViewItem tri = (TreeViewItem)e.OriginalSource;
			tri.Items.Clear();


			var d = service.GetClientDirectoriesAsync(ActiveClient.PcName, tri.Tag.ToString()).Result;
			var f = service.GetClientFilesAsync(ActiveClient.PcName, tri.Tag.ToString()).Result;

			foreach (var dir in d)
			{
				TreeViewItem new_item = new TreeViewItem();
				new_item.Tag = dir.FullName;
				new_item.Header = dir.Name;
				new_item.Expanded += New_item_Expanded;
				if (dir.HaveSub)
					new_item.Items.Add(new TreeViewItem());
			}
			foreach (var file in f)
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
		private void CheckBox_Checked(object sender, RoutedEventArgs e)
		{
			Back.Clear();
			foreach (CheckBox box in ClientsPCListBox.Items)
			{
				Back.Add(box.Content.ToString(), (bool)box.IsChecked);
				if (box.IsChecked == false) box.IsChecked = true;
			}
		}

		private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
		{
			foreach (CheckBox box in ClientsPCListBox.Items)
				if (Back.ContainsKey(box.Content?.ToString()))
				{
					box.IsChecked = Back[box.Content.ToString()];
					Back.Remove(box.Content.ToString());
					if (Back.Count == 0) break;
				}
		}

		public SuperImage GetScreenshot()
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

		public SuperProcess[] GetProcesses()
		{
			return null;
		}

		public void CloseProcess(int ProcessId)
		{
		}

		public string[] GetDrives()
		{
			return null;
		}

		public SuperFileDirectoryInfo[] GetFiles(string path)
		{
			return null;
		}

		public SuperFileDirectoryInfo[] GetDirectories(string path)
		{
			return null;
		}

		public void RemoveFile(string path)
		{
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

        SuperDrive[] IService1Callback.GetDrives()
        {
			return null;
        }

        private void OpenProcesses(object sender, RoutedEventArgs e)
        {
			new Thread(() => CallProcesses()).Start();
		}

		private async void CallProcesses()
		{
			await Task.Run(() => Dispatcher.Invoke(()=> GoAnotherAdditionMode(AdditionType.Process)));
			var r = service.GetClientProcessesAsync(ActiveClient.PcName).Result;
			await Task.Run(() => Dispatcher.Invoke(() => ProcessList.ItemsSource = r));
			ProcessWatchingTimer.Stop();
			ProcessWatchingTimer.Start();
		}

		private void MenuItem_Click_1(object sender, RoutedEventArgs e)
		{
			switch (startupitem.IsChecked)
			{
				case true:
					try
					{
						reg.SetValue("RNA_Admin", ExePath);
					}
					catch { }
					break;
				case false:
					try
					{
						reg.DeleteValue("RNA_Admin");
					}
					catch { }
					break;
			}
		}

		public string Str()
		{
			return null;
		}
		[Obsolete]
		private void Window_Loaded(object sender, RoutedEventArgs e)
		{
			Loginning();
			ConnectingToService(); 
			SetTimers();
			CheckingReg();
			CheckingMail();
			GettingUsers();
		}

		private void MenuItem_Click_2(object sender, RoutedEventArgs e)
		{
			bool value = safemodeitem.IsChecked;
			addition.SetValue("SafeMode", value);
			switch (value)
			{
				case true:
					ProtectionTimer.Start();
					break;
				case false:
					ProtectionTimer.Stop();
					break;
			}
		}

		private void sendmaillogsitem_Click(object sender, RoutedEventArgs e)
		{
			bool value = sendmaillogsitem.IsChecked;
			addition.SetValue("MailLogs", value);
			service.ChangeSMTPLoggingAsync(value);
		}

		private void MenuItem_Click_3(object sender, RoutedEventArgs e)
		{
			bool value = textlogsitem.IsChecked;
			addition.SetValue("TextLogs", value);
			service.ChangeTXTLoggingAsync(value);
		}

		public bool Ping()
		{
			return true;
		}



		private void Button_Click_1(object sender, RoutedEventArgs e)
		{
			MessageWritingWindow mwm = new MessageWritingWindow(true);
			mwm.MessageSendingEvent += ServiceMessageSending;
			mwm.ShowDialog();
		}

		private void ServiceMessageSending(string text, bool IsForAll)
		{
			if (!IsForAll) service.SendMessageToClientsAsync(ActiveClient.PcName, text);
			else service.SendMessageToClientsAsync(null, text);
		}

		private void MenuItem_Click_4(object sender, RoutedEventArgs e)
		{
			MessageWritingWindow mwm = new MessageWritingWindow(false);
			mwm.MessageSendingEvent += ServiceMessageSending;
			mwm.ShowDialog();
		}

		private async void ProcessList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			await Task.Run(() => ProcessWatchingTimer.Stop());
			await Task.Run(() => ProcessWatchingTimer.Start());
		}

		private void Button_Click_2(object sender, RoutedEventArgs e)
		{
			service.RebootPCsAsync(null);
		}

		private void Button_Click_3(object sender, RoutedEventArgs e)
		{
			service.DisconnectClientAsync(null);
		}

		private void ProcessList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			ProcessWatchingTimer.Stop();
		}

		private void ProcessList_PreviewMouseUp(object sender, MouseButtonEventArgs e)
		{
			ProcessWatchingTimer.Start();
		}

		private void ProcessList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
		{
			ProcessWatchingTimer.Stop();
			ProcessWatchingTimer.Start();
		}

		private void MenuItem_Click_5(object sender, RoutedEventArgs e)
		{
			try
			{
				if (ProcessList.SelectedItem != null)
				{
					service.CloseClientProcessAsync(ActiveClient.PcName, (ProcessList.SelectedItem as SuperProcess).Id);
					ProcessList.Items.Remove(ProcessList.SelectedItem);
				}
			}
			catch { }
		}
	}
}