using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using System;
using LitJson_Gamedonia;


public class Push : MonoBehaviour {

	public Texture2D backgroundImg;
	public GUISkin skin;


	private string errorMsg = "";
	private string statusMsg = "";
	private string console = "";

	void Awake() {

	}

	void Start() {


		if (  Gamedonia.INSTANCE== null) {

			statusMsg = "Missing Api Key/Secret. Check the README.txt for more info.";
			return;
		}
		else if (GamedoniaPushNotifications.Instance.androidSenderId == "") {
			Debug.Log ("Missing Android Sender Id, push notifications won't work for Android. Check the README.txt for more info.");
		}

		GamedoniaUsers.Authenticate(OnLogin);
		printToConsole ("Starting session with Gamedonia...");

		//Handle push
		GDPushService pushService = new GDPushService();
		pushService.RegisterEvent += new RegisterEventHandler(OnNotification);
		GamedoniaPushNotifications.AddService(pushService);

	}

	void OnGUI () {

		GUI.skin = skin;

		GUI.DrawTexture(UtilResize.ResizeGUI(new Rect(0,0,320,480)),backgroundImg);

		GUI.enabled = (statusMsg == "");

		//Text area control
		GUI.Label(UtilResize.ResizeGUI(new Rect(80,10,220,20)),"Console Log:","LabelBold");
		GUI.Box (UtilResize.ResizeGUI (new Rect (80, 30, 220, 380)), console);

		//Server Push
		if (GUI.Button (UtilResize.ResizeGUI(new Rect (80,420, 220, 50)), "Generate Push With Server Code")) {
			generatePushWithServerCode();
		}

		if (errorMsg != "") {
			GUI.Box (new Rect ((Screen.width - (UtilResize.resMultiplier() * 260)),(Screen.height - (UtilResize.resMultiplier() * 50)),(UtilResize.resMultiplier() * 260),(UtilResize.resMultiplier() * 50)), errorMsg);
			if(GUI.Button(new Rect (Screen.width - 20,Screen.height - UtilResize.resMultiplier() * 45,16,16), "x","ButtonSmall")) {
				errorMsg = "";
			}
		}

		GUI.enabled = true;
		if (statusMsg != "") {
			GUI.Box (UtilResize.ResizeGUI(new Rect (80, 240 - 40, 220, 40)), statusMsg);
		}
	}


	void generatePushWithServerCode() {
		printToConsole("Requesting server to send push...");
		GamedoniaScripts.Run("sendpush",new Dictionary<string,object>(),
		    delegate (bool success, object data) {
				if (success) {
					printToConsole("Push requested successfully");
					checkEditor ();
				}else {
					printToConsole("Failed request for server push");
				}
			}
		);
	}

	private void checkEditor() {

		if (Application.isEditor) {
			statusMsg = "Push notifications can only be received in a device. Not in Editor mode.";
		}
	}

	private void printToConsole(string msg) {
		console += msg + "\n";
	}

	void OnLogin (bool success) {

		statusMsg = "";
		if (success) {
			printToConsole("Session started successfully. uid: " + GamedoniaUsers.me._id);
		}else {
			errorMsg = Gamedonia.getLastError().ToString();
			Debug.Log(errorMsg);
		}

	}

	void OnNotification(IDictionary notification) {
		printToConsole("Notification Received: " + JsonMapper.ToJson(notification));
	}

}
