using BepInEx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using BepInEx.Logging;

namespace LCYeahTruck {
    [BepInPlugin(mod_guid, mod_name, mod_version)]
    public class YeahTruckBase : BaseUnityPlugin {
		private const string mod_guid = "raptureawaits.yeahtruck";
		private const string mod_name = "YeahTruck";
		private const string mod_version = "1.0.0";
		
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
		public HashSet<int> flipped_vehicles = new HashSet<int>();
		
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

		[HarmonyPatch("Update")]
		[HarmonyPostfix]
		static void UpdatePostfix(VehicleController __instance, ref Quaternion ___syncedRotation, ref AudioSource ___radioAudio) {
			double vehicle_angle = System.Math.Abs(___syncedRotation.eulerAngles[2] - 180.0);
			int vid = __instance.gameObject.GetInstanceID();

			if (!b.flipped_vehicles.Contains(vid) && vehicle_angle < b.flip_threshold) {
				b.flipped_vehicles.Add(vid);
				___radioAudio.PlayOneShot(b.yeah_clip);
				modlog.LogInfo("Truck has been flipped! YEEEEEEEEEEEEEEEEEEEEEEAAAAAAAAAAAAAAAA-");
			} else if (b.flipped_vehicles.Contains(vid) && vehicle_angle > b.correct_threshold) {
				b.flipped_vehicles.Remove(vid);
				modlog.LogInfo("Truck has been corrected.");
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
			b.flipped_vehicles.Clear();
			modlog.LogInfo("Reset flipped vehicles.");
		}
	}
}