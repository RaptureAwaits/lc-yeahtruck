using BepInEx;
using System;
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
		private AudioClip yeah_clip;
		public AudioClip YeahClip {
			get { return yeah_clip; }
			set { yeah_clip = value; }
		}
		public double flipThreshold = 120.0;
		public double correctThreshold = 150.0;
		private bool upside_down;
		public bool TruckUpsideDown {
			get { return upside_down; }
			set { upside_down = value; }
		}
		
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

			AudioClip[] ye_list = new_sounds.LoadAssetWithSubAssets<AudioClip>("assets\\audio\\ye_fade.wav");
			if (ye_list != null && ye_list.Length > 0) {
				AudioClip ye_clip = ye_list[0];
				instance.YeahClip = ye_clip;
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
		static void YeahPatch(ref Quaternion ___syncedRotation, ref AudioSource ___radioAudio) {
			double zenith_angle = System.Math.Abs(___syncedRotation.eulerAngles[2] - 180.0);

			if (!b.TruckUpsideDown && zenith_angle < b.flipThreshold) {
				b.TruckUpsideDown = true;
				___radioAudio.PlayOneShot(b.YeahClip);
				modlog.LogInfo("Truck has been flipped! YEEEEEEEEEEEEEEEEEEEEEEAAAAAAAAAAAAAAAA-");
			} else if (b.TruckUpsideDown && zenith_angle > b.correctThreshold) {
				b.TruckUpsideDown = false;
				modlog.LogInfo("Truck has been corrected.");
			}
		}
	}
}