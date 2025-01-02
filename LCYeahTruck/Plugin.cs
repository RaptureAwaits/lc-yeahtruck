using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using BepInEx.Logging;
using GameNetcodeStuff;

namespace LCYeahTruck {
    [BepInPlugin(mod_guid, mod_name, mod_version)]
    public class YeahTruckBase : BaseUnityPlugin {
		private const string mod_guid = "raptureawaits.yeahtruck";
		private const string mod_name = "YeahTruck";
		private const string mod_version = "1.1.0";
		
		internal static YeahTruckBase instance;
		internal static ManualLogSource modlog;
		
		private readonly Harmony harmony = new(mod_guid);

		public static AssetBundle new_sounds;
		private AudioClip _yeah_clip;
		public AudioClip yeah_clip {
			get { return _yeah_clip; }
			set { _yeah_clip = value; }
		}
		public double flip_threshold = 135.0;
		public double correct_threshold = 150.0;
		public TimeSpan cooldown_seconds = new TimeSpan(0, 0, 5);
		public HashSet<int> cool_vehicles = new HashSet<int>();
		public Dictionary<int, DateTime> cooldowns = new Dictionary<int, DateTime>();
		
        private void Awake() {
			if (instance == null) {
				instance = this;
			}
			modlog = BepInEx.Logging.Logger.CreateLogSource("YeahTruck");

			string mod_dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			new_sounds = AssetBundle.LoadFromFile(Path.Combine(mod_dir, "yeah-truck"));
			if (new_sounds == null) {
				modlog.LogError("Failed to load AssetBundle.");
				return;
			}

			AudioClip[] yl = new_sounds.LoadAssetWithSubAssets<AudioClip>("assets\\audio\\ye_fade.wav");
			if (yl != null && yl.Length > 0) {
				AudioClip yc = yl[0];
				instance.yeah_clip = yc;
			} else {
				modlog.LogError("Failed to load YEAH audio data from extracted assets.");
			}
			
			harmony.PatchAll(typeof(Patches.YeahTruckPatch));
            modlog.LogInfo($"Plugin {mod_guid} is loaded!");
        }
    }
}

namespace LCYeahTruck.Patches {
	[HarmonyPatch(typeof(VehicleController))]
	internal class YeahTruckPatch {
		internal static ManualLogSource modlog = YeahTruckBase.modlog;
		internal static YeahTruckBase b = YeahTruckBase.instance;

		[HarmonyPatch("Start")]
		[HarmonyPostfix]
		static void StartPostfix(VehicleController __instance) {
			int vid = __instance.gameObject.GetInstanceID();
			b.cool_vehicles.Add(vid);  // Truck may start airborne, so it must transition to boring before it can properly become cool
		}
			

		[HarmonyPatch("Update")]
		[HarmonyPostfix]
		static void UpdatePostfix(
			VehicleController __instance, ref Quaternion ___syncedRotation, ref AudioSource ___radioAudio, ref PlayerControllerB ___currentDriver,
			ref WheelCollider ___FrontLeftWheel, ref WheelCollider ___FrontRightWheel, ref WheelCollider ___BackLeftWheel, ref WheelCollider ___BackRightWheel
		) {
			int vid = __instance.gameObject.GetInstanceID();

			bool is_on_cooldown = b.cooldowns.ContainsKey(vid);
			// Check if the vehicle's audio cooldown has expired, and remove the vehicle from the dict if it has
			if (is_on_cooldown && DateTime.Now > b.cooldowns[vid])  {
				b.cooldowns.Remove(vid);
				is_on_cooldown = false;
			}

			bool can_vehicle_be_cool = !b.cool_vehicles.Contains(vid)  && ___currentDriver != null;

			// Check the vertical angle of the vehicle against the flip threshold
			double vehicle_angle = System.Math.Abs(___syncedRotation.eulerAngles[2] - 180.0);
			if (vehicle_angle < b.flip_threshold && can_vehicle_be_cool) {
				b.cool_vehicles.Add(vid);
				if (!is_on_cooldown) {
					___radioAudio.PlayOneShot(b.yeah_clip);
					modlog.LogInfo("Truck has been flipped! YEEEEEEEEEEEEEEEEEEEEEEAAAAAAAAAAAAAAAA-");
					b.cooldowns.Add(vid, DateTime.Now + b.cooldown_seconds);
				}
			}

			// Check the isGrounded property of each wheel
			bool is_airborne = !___FrontLeftWheel.isGrounded && !___FrontRightWheel.isGrounded && !___BackLeftWheel.isGrounded && !___BackRightWheel.isGrounded;
			if (is_airborne && can_vehicle_be_cool) {
				b.cool_vehicles.Add(vid);
				if (!is_on_cooldown) {
					___radioAudio.PlayOneShot(b.yeah_clip);
					modlog.LogInfo("Truck is airborne! YEEEEEEEEEEEEEEEEEEEEEEAAAAAAAAAAAAAAAA-");
					b.cooldowns.Add(vid, DateTime.Now + b.cooldown_seconds);
				}
			}

			// Remove the vehicle from the cool list if it is not airborne AND not flipped
			bool is_grounded = ___FrontLeftWheel.isGrounded && ___FrontRightWheel.isGrounded && ___BackLeftWheel.isGrounded && ___BackRightWheel.isGrounded;
			if (b.cool_vehicles.Contains(vid) && vehicle_angle > b.correct_threshold && is_grounded) {
				b.cool_vehicles.Remove(vid);
				modlog.LogInfo("Truck is boring again.");
			}
		}
	}

	[HarmonyPatch(typeof(StartOfRound))]
	internal class FlipResetPatch {
		internal static ManualLogSource modlog = YeahTruckBase.modlog;
		internal static YeahTruckBase b = YeahTruckBase.instance;

		[HarmonyPatch("Start")]
		[HarmonyPostfix]
		static void StartPostfix() {
			b.cool_vehicles.Clear();
			b.cooldowns.Clear();
			modlog.LogInfo("Reset cool vehicles.");
		}
	}
}