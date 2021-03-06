﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;
using System.Windows;

namespace zombiesnu.DayZeroLauncher.App.Core
{
	public class DayZUpdater : BindableBase
	{
		private bool _isChecking;
		private HashWebClient.RemoteFileInfo _lastModsJsonLoc;

		public DayZUpdater(GameLauncher gameLauncher)
		{
			Downloader = new TorrentLauncher(gameLauncher);
			Downloader.PropertyChanged += (sender, args) =>
				{
					if (args.PropertyName == "IsRunning")
					{
						PropertyHasChanged("InstallButtonVisible");
					}
					else if (args.PropertyName == "Status")
					{
						if (Downloader.Status == DayZeroLauncherUpdater.STATUS_INSTALLCOMPLETE)
						{
							CheckForUpdates(_lastModsJsonLoc);
						}
					}
				};
		}

		public TorrentLauncher Downloader { get; set; }

		public bool VersionMismatch
		{
			get
			{
				if (CalculatedGameSettings.Current.ModContentVersion == null)
					return true;
				if (LatestVersion == null)
					return false;

				return !CalculatedGameSettings.Current.ModContentVersion.Equals(LatestVersion,StringComparison.OrdinalIgnoreCase);
			}
		}

		public class ModsMeta
		{
			public class ModInfo
			{
				[JsonProperty("version")]
				public string Version = null;

				[JsonProperty("archive")]
				public HashWebClient.RemoteFileInfo Archive = null;
			}

			[JsonProperty("mods")]
			public List<ModInfo> Mods = null;
			[JsonProperty("latest")]
			public string LatestModVersion = null;

			public static string GetFileName()
			{
				string modsFileName = Path.Combine(UserSettings.ContentMetaPath, "index.json");
				return modsFileName;
			}

			public static ModsMeta LoadFromFile(string fileFullPath)
			{
				ModsMeta modsInfo = JsonConvert.DeserializeObject<ModsMeta>(File.ReadAllText(fileFullPath));
				return modsInfo;
			}
		}
	
		public void CheckForUpdates(HashWebClient.RemoteFileInfo mods)
		{
			if (_isChecking)
				return;

			_lastModsJsonLoc = mods;
			_isChecking = true;

			new Thread(() =>
			{
				try
				{
					string modsFileName = ModsMeta.GetFileName();
					ModsMeta modsInfo = null;

					HashWebClient.DownloadWithStatusDots(mods,modsFileName,DayZeroLauncherUpdater.STATUS_CHECKINGFORUPDATES,
						(newStatus) => 
							{ 
								Status = newStatus; 
							},
						(wc,fileInfo,destPath) => 
							{
								modsInfo = ModsMeta.LoadFromFile(modsFileName);
							});

					if (modsInfo != null)
					{
						Status = DayZeroLauncherUpdater.STATUS_CHECKINGFORUPDATES;
						Thread.Sleep(100);

						try
						{
							ModsMeta.ModInfo theMod = modsInfo.Mods.Where(x => x.Version.Equals(modsInfo.LatestModVersion, StringComparison.OrdinalIgnoreCase)).Single();
							SetLatestModVersion(theMod);

							string currVersion = CalculatedGameSettings.Current.ModContentVersion;
							if (!theMod.Version.Equals(currVersion, StringComparison.OrdinalIgnoreCase))
							{
								Status = DayZeroLauncherUpdater.STATUS_OUTOFDATE;

								//this lets them seed/repair version they already have if it's not discontinued
								ModsMeta.ModInfo currMod = modsInfo.Mods.SingleOrDefault(x => x.Version.Equals(currVersion, StringComparison.OrdinalIgnoreCase));
								if (currMod != null)
									DownloadSpecificVersion(currMod, false);
								else //try getting it from file cache (necessary for switching branches)
									DownloadLocalVersion(currVersion, false);
							}
							else
							{
								Status = DayZeroLauncherUpdater.STATUS_UPTODATE;
								DownloadLatestVersion(false);
							}							
						}
						catch (Exception) { Status = "Could not determine revision"; }
					}
				}
				finally { _isChecking = false; }
			}).Start();
		}

		ModsMeta.ModInfo _latestModVersion = null;
		private void SetLatestModVersion(ModsMeta.ModInfo newModInfo)
		{
			_latestModVersion = newModInfo;
			Execute.OnUiThread(() => PropertyHasChanged("LatestVersion", "VersionMismatch", "InstallButtonVisible", "VerifyButtonVisible"));
		}
		public string LatestVersion
		{
			get
			{
				if (_latestModVersion != null)
					return _latestModVersion.Version;

				return null;
			}
		}
		public void DownloadLatestVersion(bool forceFullSystemsCheck)
		{
			if (LatestVersion == null)
			{
				MessageBox.Show("Please check for new versions first", "Unable to determine latest version", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}

			Downloader.StartFromNetContent(_latestModVersion.Version,forceFullSystemsCheck,_latestModVersion.Archive,this, false);
		}
		public void DownloadSpecificVersion(ModsMeta.ModInfo modInfo, bool forceFullSystemsCheck)
		{
			Downloader.StartFromNetContent(modInfo.Version, forceFullSystemsCheck, modInfo.Archive, this, true);
		}

		public void DownloadLocalVersion(string versionString, bool forceFullSystemsCheck)
		{
			Downloader.StartFromContentFile(versionString, forceFullSystemsCheck, this);
		}

		private string _status;
		public string Status
		{
			get { return _status; }
			set
			{
				_status = value;
				Execute.OnUiThread(() => PropertyHasChanged("Status", "VersionMismatch", "InstallButtonVisible", "VerifyButtonVisible"));
			}
		}

		public bool InstallButtonVisible
		{
			get { return VersionMismatch && !_isChecking && !Downloader.RunningForVersion(LatestVersion); }
		}

        public bool VerifyButtonVisible
        {
			get { return !VersionMismatch && !Downloader.IsRunning; }
        } 
	}
}