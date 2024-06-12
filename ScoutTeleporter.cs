using OWML.Common;
using OWML.ModHelper;
using ScoutTeleporter.Utilities.ModAPIs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ScoutTeleporter
{
    public class ScoutTeleporter : ModBehaviour
    {
        public static INewHorizons newHorizonsAPI;
        public static ScoutTeleporter Instance;
        public static bool reset;
        public ScreenPrompt _teleportPrompt;

        private GameObject _whiteHole;
        private GameObject _blackHole;

        public GameObject WhiteHole => _whiteHole;
        public GameObject BlackHole => _blackHole;
        public OWRigidbody ProbeBody => Locator.GetProbe().GetComponent<OWRigidbody>();
        public PlayerBody PlayerBody => Locator.GetPlayerBody() as PlayerBody;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            newHorizonsAPI = ModHelper.Interaction.TryGetModApi<INewHorizons>("xen.NewHorizons");
            newHorizonsAPI.LoadConfigs(this);
            newHorizonsAPI.GetStarSystemLoadedEvent().AddListener(OnStarSystemLoaded);
            ModHelper.Console.WriteLine($"{nameof(ScoutTeleporter)} is loaded!", MessageType.Success);
        }

        public void UpdatePromptVisibility()
        {
            var probe = Locator.GetProbe();

            if (reset || !probe.enabled)
            {
                _teleportPrompt.VisibilityControllerSetVisibility(_teleportPrompt, false);
            }
            else if ((probe.IsAnchored() || probe.IsLaunched()) && !reset)
            {
                _teleportPrompt.VisibilityControllerSetVisibility(_teleportPrompt, true);
            }
        }

        private void OnStarSystemLoaded(string systemName)
        {
            ModHelper.Console.WriteLine("LOADED SYSTEM " + systemName);
            if (systemName == "SolarSystem")
            {
                ModHelper.Events.Unity.RunWhen(() => Locator.GetProbe() != null && Locator.GetPlayerTransform() != null, Spawn);
            }
        }

        public RelativeLocationData RelativeLocationData => new RelativeLocationData
        {
            localPosition = BlackHole.transform.InverseTransformPoint(PlayerBody.transform.position),
            localRotation = Quaternion.Inverse(BlackHole.transform.rotation) * PlayerBody.transform.rotation,
            localRelativeVelocity = Vector3.zero
        };

        public void WarpToProbe()
        {
            if (_whiteHole == null) return;
            if (PlayerState.IsInsideShip() || PlayerState.IsInsideShuttle()) return;
            GlobalMessenger.FireEvent("PlayerEnterBlackHole");
            _whiteHole.GetComponentInChildren<WhiteHoleVolume>(true).ReceiveWarpedBody(PlayerBody, RelativeLocationData);
        }

        public void Spawn()
        {
            var probeTransform = Locator.GetProbe().transform;
            probeTransform.Find("ProbeGravity/Props_NOM_GravityCrystal").transform.localScale = new UnityEngine.Vector3(0.1f, 0.1f, 0.1f);
            probeTransform.Find("ProbeGravity/Props_NOM_GravityCrystal_Base").transform.localScale = new UnityEngine.Vector3(0.1f, 0.1f, 0.1f);

            var WH = probeTransform.Find("WhiteHoleTeleport").gameObject;
            var BH = Locator.GetPlayerTransform().Find("BlackHoleTeleport").gameObject;

            _whiteHole = WH;
            _blackHole = BH;

            Object.DestroyImmediate(BH.GetComponentInChildren<BlackHoleVolume>(true).gameObject);

            BH.transform.parent = probeTransform.parent;

            WH.SetActive(false);
            BH.SetActive(false);

            ScoutTeleporter.Instance.ModHelper.Events.Unity.FireOnNextUpdate(() =>
            {
                _teleportPrompt = new ScreenPrompt(newHorizonsAPI.GetTranslationForUI("TELEPORT_PROMPT") + " <CMD>", GetButtonSprite(KeyCode.T));
                Locator.GetPromptManager().AddScreenPrompt(_teleportPrompt, PromptPosition.UpperRight, false);
                reset = false;
            });
            ModHelper.Console.WriteLine("Parenting done!", MessageType.Success);
        }

        public void EnableWhiteHole()
        {
            WhiteHole.SetActive(true);
            Invoke("DisableWhiteHole", 0.5f);
        }

        public void DisableWhiteHole()
        {
            WhiteHole.SetActive(false);
        }

        public void EnableBlackHole()
        {
            var probeBody = ProbeBody;
            var playerBody = PlayerBody;

            playerBody._currentAngularVelocity = probeBody._currentAngularVelocity;
            playerBody._currentAccel = probeBody._currentAccel;
            playerBody._currentVelocity = new UnityEngine.Vector3(0f, 0f, 0f);
            playerBody._currentVelocity = probeBody._currentVelocity;

            var BH = BlackHole;
            BH.transform.position = playerBody.transform.position;
            BH.SetActive(true);

            Invoke("WarpToProbe", 0.25f);

            reset = true;

            Invoke("DisableBlackHole", 0.5f);
        }

        public void DisableBlackHole()
        {
            BlackHole.SetActive(false);
            reset = true;
        }

        public void ResetTimer()
        {
            reset = false;
            Locator.GetPlayerAudioController().PlayEnterLaunchCodes();
        }

        public void Update()
        {
            var probe = Locator.GetProbe();
            var playerBody = PlayerBody;

            if (probe == null || playerBody == null) return;

            var playerRigidbody = playerBody.GetComponent<Rigidbody>();

            if (!reset && (probe.IsAnchored() || probe.IsLaunched()) && playerBody._activeRigidbody == playerRigidbody && Keyboard.current[Key.T].wasReleasedThisFrame)
            {
                Invoke("EnableWhiteHole", 0.2f);
                Invoke("EnableBlackHole", 0.3f);
                Invoke("ResetTimer", 5f);
                ModHelper.Console.WriteLine("Teleported!", MessageType.Success);
            }

            UpdatePromptVisibility();
        }

        public static Sprite GetButtonSprite(JoystickButton button) => GetButtonSprite(ButtonPromptLibrary.SharedInstance.GetButtonTexture(button));
        public static Sprite GetButtonSprite(KeyCode key) => GetButtonSprite(ButtonPromptLibrary.SharedInstance.GetButtonTexture(key));
        private static Sprite GetButtonSprite(Texture2D texture)
        {
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, Vector4.zero, false);
            sprite.name = texture.name;
            return sprite;
        }
    }
}