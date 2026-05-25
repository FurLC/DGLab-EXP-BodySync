using UnityEngine;

namespace DGLab.BepInEx
{
    public sealed class DGLabImGuiRunner : MonoBehaviour
    {
        public DGLabPlugin Owner;
        private bool _updateLogged;
        private bool _guiLogged;

        public bool HasUpdated { get; private set; }
        public bool HasDrawnGui { get; private set; }

        public void Awake()
        {
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(gameObject);
        }

        public void Start()
        {
            if (!ReferenceEquals(Owner, null)) Owner.LogRunnerInfo("DG-Lab IMGUI runner Start invoked. Active=" + gameObject.activeInHierarchy + ", Enabled=" + enabled);
        }

        public void Update()
        {
            HasUpdated = true;
            if (!_updateLogged)
            {
                _updateLogged = true;
                if (!ReferenceEquals(Owner, null)) Owner.LogRunnerInfo("DG-Lab IMGUI runner Update invoked.");
            }

            if (!ReferenceEquals(Owner, null)) Owner.HostedMenuUpdate("DGLabImGuiRunner.Update");
        }

        public void OnGUI()
        {
            HasDrawnGui = true;
            if (!_guiLogged)
            {
                _guiLogged = true;
                if (!ReferenceEquals(Owner, null)) Owner.LogRunnerInfo("DG-Lab IMGUI runner OnGUI invoked.");
            }

            if (!ReferenceEquals(Owner, null)) Owner.HostedMenuOnGUI("DGLabImGuiRunner.OnGUI");
        }
    }
}
