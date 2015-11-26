// ------------------------------------------------------------------------------
//  <autogenerated>
//      This code was generated by a tool.
//      Mono Runtime Version: 4.0.30319.1
// 
//      Changes to this file may cause incorrect behavior and will be lost if 
//      the code is regenerated.
//  </autogenerated>
// ------------------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Net.Sockets;
using System.Text;
using MiniJSON_Gamedonia;
using LitJson_Gamedonia;
using HTTP;

namespace Gamedonia.Backend {

	public class Download: RequestDelegate
	{


		private const string DOWNLOAD_DID_FINISH_LOADING = "downloadDidFinishLoading";
		private const string DOWNLOAD_DID_FAIL = "downloadDidFail";
		private const string DOWNLOAD_DID_FAIL_CONNECTIVITY = "downloadDidFailConnectivity";
		private const string DOWNLOAD_DID_FAIL_NO_AVAILABLE_SPACE = "downloadDidFailNoAvailableSpace";
		private const string DOWNLOAD_DID_RECEIVE_DATA = "downloadDidReceiveData";
		private const string DOWNLOAD_DID_START = "downloadDidStart";
		private const string DOWNLOAD_DID_PAUSE = "downloadDidPause";

		private string filename;
		private string url;
		private bool downloading = false;
		private DownloadDelegate downloadDelegate;
		private long expectedContentLength = 0;
		private long progressContentLength = 0;
		private string error;
		private string fileId;
		private string tempFilename;
		private Request request = null;
		private bool connectionCleanedup = false;
		public string filesystemPath;


		//TODO
		//private var _downloadStream:FileStream;
		//private var _connection:URLStream;
		private NetworkStream networkStream; 
		private FileStream downloadStream;
		private Socket client;

		private bool output = false;
		private bool resume = false;

		private bool excludeUntilResume = false;

		public Download ()
		{
		}

		public void init(string filename, string url, string fileId) {

			this.filename = filename;
			this.url = url;
			this.fileId = fileId;
			
		}


		private void GetFileConentLength(string tempFilename, string url, Action<long>callback) {

			GamedoniaBackend.RunCoroutine(InternalGetFileConentLength(tempFilename,url, callback));
		}


		private IEnumerator InternalGetFileConentLength(string tempFilename, string url, Action<long>callback) {

			request = new Request ("GET", this.url);
			request.Send(this.tempFilename, true);

			while (!request.isDone)
			{
				yield return request;
			}
			
			if ((request.response.status == 200) || (request.response.status == 206)) {
				//Debug.Log (">>>> Content Length: " + request.response.contentLength);
				callback(request.response.contentLength);
			}else {
				callback(0);
			}
		}


		private void DownloadFile(string tempFilename, string url, long downloadedBytes) {

			GamedoniaBackend.RunCoroutine (InternalDownloadFile (tempFilename, url, downloadedBytes));

		}

		private IEnumerator InternalDownloadFile(string tempFilename, string url, long downloadedBytes) {

			request = new Request ("GET", this.url);
			request.downloadDelegate = this;

			if (downloadedBytes > 0) request.SetHeader ("Range", "bytes=" + downloadedBytes + "-");

			if (request == null) {
				this.error = "Unable to create URL " + this.url;
				this.cleanupConnectionSuccessful (false);
				yield return null;
			}

			dispatchDownloadEvent (DOWNLOAD_DID_START, this);

			//Debug.Log ("Before download");
			request.Send (this.tempFilename);
			//Debug.Log ("After download");
		
			while (!request.isDone) {
				yield return request;
			}

			//Debug.Log ("Request finished XXX");
			if ((request.response.status == 200) || (request.response.status == 206)) {
				//Debug.Log ("[DownloadManager] Download complete");
				if (!request.response.ForceStop) cleanupConnectionSuccessful (true);
			} else if (request.response.status != -100) {
				Debug.LogError ("Download failed " + request.response.status + " " + request.response.message);
				cleanupConnectionSuccessful (false);
			}
		}

		#if UNITY_WEBPLAYER
		public void Start() {
			Debug.LogWarning ("DownloadManager not supported on WebPlayer");
		}
		#else

		public void Start() {
			

			// initialize progress variables			
			this.downloading = true;
			this.expectedContentLength = -1;
			this.progressContentLength = 0;

			this.tempFilename = this.GetFileSystemPath() + "/tmp/" + url.Substring(url.LastIndexOf("/")+1, (url.LastIndexOf("?") - url.LastIndexOf("/") - 1));

			string directoryPath = Path.GetDirectoryName(this.tempFilename);
			if (!Directory.CreateDirectory (directoryPath).Exists) {
				this.cleanupConnectionSuccessful(false);
				return;
			}
			//Debug.Log ("Tempfilename: " + this.tempFilename);

			//Miramos y calculamos la cantidad de bytes ya descargados
			long downloadedBytes = 0;

			if (File.Exists(tempFilename)) {
				FileInfo tempFileInfo = new FileInfo (this.tempFilename);
				downloadedBytes = tempFileInfo.Length;
			}

			if (downloadedBytes > 0) {
					if (GamedoniaFiles.Instance.debug) Debug.Log ("[DownloadManager] Resuming download for file " + tempFilename);				
					this.progressContentLength = downloadedBytes;


					this.GetFileConentLength(tempFilename,url,
				        delegate (long contentLength) {								
							this.expectedContentLength = contentLength;				
							if (this.expectedContentLength < this.progressContentLength) {
								this.cleanupConnectionSuccessful(false);
								return;
							}
							this.DownloadFile(tempFilename,url, downloadedBytes);
						}
				    );

					//TODO Calculo del espacio disponible
					//Check available space

			}else {
				if (GamedoniaFiles.Instance.debug) Debug.Log ("[DownloadManager] Starting download for file " + tempFilename);
				this.DownloadFile(tempFilename,url, downloadedBytes);
			}
				
		}

		#endif

		public void Cancel() {
			Debug.Log ("[DownloadManager] Cancel download");
			this.request.Stop ();
			this.cleanupConnectionSuccessful(false);
		}
		
		public void Pause() {
			//Debug.Log ("Pause");
			if (downloading) {
				if (GamedoniaFiles.Instance.debug) Debug.Log ("[DownloadManager] Pause download");
				this.request.Stop();
				this.cleanupConnectionSuccessful (false, 2);
			}
		}

		public string GetFileSystemPath () 
		{ 
			// Your game has read+write access to /var/mobile/Applications/XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX/Documents 
			// Application.dataPath returns              
			// /var/mobile/Applications/XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX/myappname.app/Data 
			// Strip "/Data" from path 
			//string path = Application.dataPath.Substring (0, Application.dataPath.Length - 5); 
			// Strip application name 
			//path = path.Substring(0, path.LastIndexOf('/'));  
			//return path + "/Documents"; 

			return filesystemPath;
		}


		public void OnProgress (ProgressEvent pEvent) {

			this.progressContentLength = pEvent.bytesLoaded + this.progressContentLength;

			if (GamedoniaFiles.Instance.debug) Debug.Log ("[DownloadManager] Progress Content Length: " + this.progressContentLength);

			if ((this.expectedContentLength == -1 || this.expectedContentLength == 0) && (!resume)) {
				this.expectedContentLength = pEvent.bytesTotal;						
				//Check de tamaño
				//var space:Number = 0;
				//TODO Check disk spacce

				//var space:Number = File.applicationStorageDirectory.spaceAvailable;
				//if (space < event.bytesTotal) {
				//	cleanupConnectionSuccessful(false,3);
				//}
				
				//updateDownloadPreferences(this);
			}

			dispatchDownloadEvent (DOWNLOAD_DID_RECEIVE_DATA,this);
		}

		public void OnIOError (IOErrorEvent ioError) {

			Debug.LogError ("IO Error event: " + ioError.Message);
			ConnectionChecker checker = new ConnectionChecker ();

			checker.check (
				delegate (bool success) {	
					if (success) {
						cleanupConnectionSuccessful(false,0);	
					}else {
						cleanupConnectionSuccessful(false,1);
					}
			});

		}

		public void OnResponseHeaders(ProgressEvent pEvent) {

			/*
			Debug.Log ("On Response Headers");

			this.expectedContentLength = pEvent.bytesTotal;
			Debug.Log ("Expected Content Length: " + this.expectedContentLength);
			if (this.expectedContentLength < this.progressContentLength) {
				request.Stop();
				this.cleanupConnectionSuccessful(false);
			}
			*/

		}

		#if UNITY_WEBPLAYER
		private void cleanupConnectionSuccessful(bool success, int command = 0) {
			Debug.LogWarning ("DownloadManager not supported on WebPlayer");
		}
		#else
		private void cleanupConnectionSuccessful(bool success, int command = 0) {

			if (!connectionCleanedup) {
				connectionCleanedup = true;
				FileInfo fileManager = new FileInfo (this.filename);
				FileInfo tmpFileManager = new FileInfo (this.GetFileSystemPath () + "/tmp/" + url.Substring (url.LastIndexOf ("/") + 1, (url.LastIndexOf ("?") - url.LastIndexOf ("/") - 1)));

				this.downloading = false;

				if (success) {
						if (expectedContentLength != this.progressContentLength && this.expectedContentLength != tmpFileManager.Length) {
								if (this.progressContentLength < this.expectedContentLength) {
										this.excludeUntilResume = true;
										this.error = "Incomplete download file " + fileManager.FullName;
										dispatchDownloadEvent (DOWNLOAD_DID_FAIL_CONNECTIVITY, this);
								} else {
										this.error = "Could not download the file " + fileManager.FullName;
										dispatchDownloadEvent (DOWNLOAD_DID_FAIL, this);
								}

								return;
						}

						string directoryPath = Path.GetDirectoryName (this.filename);
						if (!Directory.CreateDirectory (directoryPath).Exists) {
								this.error = "Could not create the path " + directoryPath;
								dispatchDownloadEvent (DOWNLOAD_DID_FAIL, this);
								return;
						}

						if (fileManager.Exists) {
								try {
										fileManager.Delete ();
								} catch (IOException ex) {
										Debug.LogException (ex);
										this.error = "Unable to delete file " + fileManager.FullName;
										dispatchDownloadEvent (DOWNLOAD_DID_FAIL, this);
										return;
								}
						}

						//Copiamos el temporal a la ubicacion final
						tmpFileManager.CopyTo (fileManager.FullName, true);

						//Borramos el temporal
						try {
								tmpFileManager.Delete ();
						} catch (IOException ex) {
								Debug.LogException (ex);
								this.error = "Unable to delete temp file " + fileManager.FullName;
								dispatchDownloadEvent (DOWNLOAD_DID_FAIL, this);
								return;
						}

						if (GamedoniaFiles.Instance.debug) Debug.Log ("[DownloadManager] Completed download for file " + fileManager.FullName);
						dispatchDownloadEvent (DOWNLOAD_DID_FINISH_LOADING, this);

				} else {

						//Miramos si es un descarga parcial
						switch (command) {
						case 0: //FAIL DE DESCARGA NORMAL
								if (File.Exists (this.tempFilename) && tmpFileManager.Exists)
										tmpFileManager.Delete ();
								this.error = "Could not download the file " + fileManager.FullName;
								dispatchDownloadEvent (DOWNLOAD_DID_FAIL, this);
								break;
						case 1: //FAIL DESCONEXION
								this.excludeUntilResume = true;
								this.error = "Incomplete download file " + fileManager.FullName;
								dispatchDownloadEvent (DOWNLOAD_DID_FAIL_CONNECTIVITY, this);
								break;
						case 2: // PAUSE
								this.excludeUntilResume = true;
								dispatchDownloadEvent (DOWNLOAD_DID_PAUSE, this);
								break;
		
						case 3: // NO SPACE
								if (File.Exists (tempFilename) && tmpFileManager.Exists)
										tmpFileManager.Delete ();
								this.error = "Insufficient space for the file " + fileManager.FullName;
								dispatchDownloadEvent (DOWNLOAD_DID_FAIL_NO_AVAILABLE_SPACE, this);
								break;
		
						}

				}
			}
			
		}
		#endif

		private void updateDownloadPreferences(Download download) {

			string resolvedDownloadUrlsSO = PlayerPrefs.GetString ("resolvedDownloadUrls");
			ArrayList resolvedDownloadUrls = JsonMapper.ToObject<ArrayList> (resolvedDownloadUrlsSO);

			int removeIndex = -1;
			foreach (IDictionary storedDownload in resolvedDownloadUrls) {

				if (download.fileId.Equals(storedDownload["fileId"] as string)) {
					removeIndex = resolvedDownloadUrls.IndexOf(storedDownload);
				}

			}

			if (removeIndex != -1) {
				resolvedDownloadUrls.RemoveAt(removeIndex);
			}

			PlayerPrefs.SetString ("resolvedDownloadUrls", JsonMapper.ToJson (resolvedDownloadUrls));
			PlayerPrefs.Save ();
		}

		private void dispatchDownloadEvent(string type, Download download) {

			if (downloadDelegate != null) {
				switch (type) {

					case DOWNLOAD_DID_FINISH_LOADING:
						this.downloadDelegate.downloadDidFinishLoading(download);
						break;
					case DOWNLOAD_DID_FAIL:
						this.downloadDelegate.downloadDidFail (download);	
						break;
					case DOWNLOAD_DID_FAIL_CONNECTIVITY:
						this.downloadDelegate.downloadDidFailConnectivity (download);	
						break;
					case DOWNLOAD_DID_FAIL_NO_AVAILABLE_SPACE:
						this.downloadDelegate.downloadDidFailNoAvailableSpace (download);	
						break;
					case DOWNLOAD_DID_RECEIVE_DATA:
						this.downloadDelegate.downloadDidReceiveData(download);
						break;
					case DOWNLOAD_DID_START:
						this.downloadDelegate.downloadDidStart(download);
						break;
					case DOWNLOAD_DID_PAUSE:
						this.downloadDelegate.downloadDidPause(download);
						break;

				}
			}else {
				Debug.LogWarning("No downloadDelegae defined");
			}
			
		}

		private long getExpectedContentLengthForFileId(string fileId) {
			
			
			string resolvedDownloadUrlsSO = PlayerPrefs.GetString("resolvedDownloadUrls");
			if (resolvedDownloadUrlsSO != null) { 
				ArrayList resolvedDownloadUrls = JsonMapper.ToObject<ArrayList>(resolvedDownloadUrlsSO);
				foreach(string storedDownloadJSON in resolvedDownloadUrls) {
					IDictionary storedDownload =  Json.Deserialize(storedDownloadJSON) as IDictionary;
					if (storedDownload["FileId"].Equals(fileId)) {
						object obj = storedDownload["ExpectedContentLength"];
						if (obj is long) {
							return (long)obj;
						}
					}
				}
			}
			
			return 0;
			
		}

		public bool Downloading {
			get {
				return downloading;
			}

			set {
				downloading = value;
			}
		}

		public DownloadDelegate DownloadDelegate {
			get {
				return downloadDelegate;
			}

			set {
				downloadDelegate = value;
			}
		}

		public bool ExcludeUntilResume {
			get {
				return excludeUntilResume;
			}

			set {
				excludeUntilResume = value;
			}
		}

		public string FileId {
			get {
				return fileId;
			}
		}

		public bool isDownloading() {
			return downloading;
		}

		public bool Resume {
			get {
				return resume;
			}
			set {
				resume = value;
				connectionCleanedup = false;
			}
		}

		public long ExpectedContentLength {
			get {
				return expectedContentLength;
			}
			set {
				expectedContentLength = value;
			}
		}

		public long ProgressContentLength {
			get {
				return progressContentLength;
			}
			set {
				progressContentLength = value;
			}
		}

		public string Filename {
			get {
				return filename;
			}
			set {
				filename = value;
			}
		}

		public string Url {
			get {
				return url;
			}
			set {
				url = value;
			}
		}

		public string Error {
			get {
				return error;
			}
			set {
				error = value;
			}
		}
	}

}

