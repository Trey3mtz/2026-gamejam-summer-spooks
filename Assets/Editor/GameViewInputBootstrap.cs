using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpookyGame.Editor
{
    /// <summary>
    /// Keeps the detached Game view usable as the project's primary desktop play surface.
    /// Unity otherwise defaults new Game views to Play Unfocused and may route their mouse
    /// and keyboard input back to the Editor instead of the running game.
    /// </summary>
    [InitializeOnLoad]
    internal static class GameViewInputBootstrap
    {
        private static readonly Type GameViewType =
            typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        private static readonly Type EnterPlayModeBehaviorType =
            typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView+EnterPlayModeBehavior");
        private static readonly PropertyInfo EnterPlayModeBehaviorProperty =
            GameViewType?.GetProperty("enterPlayModeBehavior",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly object PlayFocused = EnterPlayModeBehaviorType == null
            ? null
            : Enum.Parse(EnterPlayModeBehaviorType, "PlayFocused");

        static GameViewInputBootstrap()
        {
            ConfigureInputRouting();
            ConfigureOpenGameViews();

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                ConfigureInputRouting();
                ConfigureOpenGameViews();
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Game views finish restoring their layout one editor tick after Play begins.
                EditorApplication.delayCall += FocusLargestGameView;
            }
        }

        private static void ConfigureInputRouting()
        {
            if (InputSystem.settings == null)
                return;

            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior =
                InputSettings.BackgroundBehavior.IgnoreFocus;
        }

        private static void ConfigureOpenGameViews()
        {
            if (GameViewType == null || EnterPlayModeBehaviorProperty == null || PlayFocused == null)
                return;

            UnityEngine.Object[] gameViews = Resources.FindObjectsOfTypeAll(GameViewType);
            foreach (UnityEngine.Object gameView in gameViews)
                EnterPlayModeBehaviorProperty.SetValue(gameView, PlayFocused);
        }

        private static void FocusLargestGameView()
        {
            ConfigureOpenGameViews();

            if (GameViewType == null)
                return;

            UnityEngine.Object[] gameViews = Resources.FindObjectsOfTypeAll(GameViewType);
            EditorWindow bestView = null;
            float bestArea = -1f;

            foreach (UnityEngine.Object gameView in gameViews)
            {
                if (gameView is not EditorWindow window)
                    continue;

                float area = window.position.width * window.position.height;
                if (area <= bestArea)
                    continue;

                bestArea = area;
                bestView = window;
            }

            if (bestView == null)
                return;

            bestView.Focus();
            bestView.Repaint();
            RestoreGameInputDevices();
        }

        private static void RestoreGameInputDevices()
        {
            // Unity applies its focus transition after ExitingEditMode and can leave
            // desktop devices disabled even when IgnoreFocus is already selected.
            // Explicitly restore them after the Game view receives focus.
            ConfigureInputRouting();

            if (Mouse.current != null && !Mouse.current.enabled)
                InputSystem.EnableDevice(Mouse.current);
            if (Keyboard.current != null && !Keyboard.current.enabled)
                InputSystem.EnableDevice(Keyboard.current);
        }
    }
}
