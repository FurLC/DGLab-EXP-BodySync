using UnityEngine;

namespace DGLab.BepInEx
{
    internal sealed class DGLabImGuiBootstrapper : MonoBehaviour
    {
        public DGLabPlugin Owner;

        public void Awake()
        {
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(gameObject);
        }

        public void Start()
        {
            if (!ReferenceEquals(Owner, null)) Owner.LogRunnerInfo("DG-Lab IMGUI bootstrapper Start invoked.");
            Destroy(gameObject);
        }

        public void OnDestroy()
        {
            if (!ReferenceEquals(Owner, null)) Owner.EnsureStandaloneImGuiRunnerFromBootstrapper();
        }
    }
}
