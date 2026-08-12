using UnityEngine;

namespace SpookyGame.Rendering
{
    public class BillboardText : MonoBehaviour
    {
        private Camera _mainCamera;

        private void Start()
        {
            // Cache the camera reference to avoid expensive property lookups every frame
            _mainCamera = Camera.main;
        }

        private void LateUpdate()
        {
            // Safety check in case the camera is destroyed or temporarily disabled
            if (!_mainCamera) return;

            // Align the text's rotation to perfectly match the camera's rotation
            transform.rotation = _mainCamera.transform.rotation;
        }
    }
}