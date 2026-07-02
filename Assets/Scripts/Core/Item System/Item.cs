using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpookyGame
{
    [CreateAssetMenu(fileName = "Item", menuName = "Scriptable Objects/Item")]
    public class Item : ScriptableObject
    {
    // These enum are for the purpose of sorting items
        // The enums at the Top will be first, in the top left of the inventory.
        // The enums at the Bottom of this list will go to the bottom of the inventory
        public enum ItemType{
            Other,
            Key,
            Glyph,
            Consumable,
            Wallet
        }

        public string Key => UniqueID;

        [Tooltip("Name used in the database for that Item, used by save system so no [spaces]")]
        public string UniqueID = "DefaultID";

        [Tooltip("Display name that will be visible in-game, allowed to have [spaces] in the name")]        
        public string DisplayName;
        public ItemType Type;
        public Sprite ItemSprite;
        public int MaxStackSize = 1;

        // NOTE: This is just string for a tooltip. You must place Tooltip_Trigger on
        //       whatever you want to have a tooltip, and grab the item's Tooltip strings
        //       and set that Tooltip_Trigger's strings to these string.
        [Tooltip("This is a tooltip to display item's name, and description if needed")]  
        public string Tooltip_header;
        [Multiline()]
        public string Tooltip_content;

        [Tooltip("UNDETERMINED  (do you need consumables with prefabs?)")]
        public GameObject VisualPrefab;
        public string PlayerAnimatorTriggerUse = "GenericToolSwing";
        
        [Tooltip("Sound triggered when using/equiping the item")]
        public AudioClip[] UseSound;
        public Vector2 volume = new Vector2(0.5f, 0.5f);
        public Vector2 pitch = new Vector2(1,1);
                
        public bool IsStackable()
        {   return MaxStackSize > 1;    }

        // Just methods to play audioclips when you Use an item
        // public void PlaySound(Transform itemLocation)
        // {
        //     if(UseSound.Length == 0)
        //     {
        //         Debug.LogWarning($"Missing sound clips for item {DisplayName}");
        //         return;
        //     }
        //
        //     // Sends all audioclips to master audio
        //     foreach(AudioClip clip in UseSound)
        //     {   AudioManager.Instance.PlaySoundFX(clip, itemLocation);   }
        // }
        //
        // public void PlaySound(Transform itemLocation, float volume, float pitch)
        // {
        //     if(UseSound.Length == 0)
        //     {
        //         Debug.LogWarning($"Missing sound clips for item {DisplayName}");
        //         return;
        //     }
        //
        //     // Sends all audioclips to master audio
        //     foreach(AudioClip clip in UseSound)
        //     {   AudioManager.Instance.PlaySoundFX(clip, itemLocation, volume, pitch);   }
        // }
        //
        // public void PlayUISound(Transform itemLocation, float volume, float pitch)
        // {
        //     if(UseSound.Length == 0)
        //     {
        //         Debug.LogWarning($"Missing sound clips for item {DisplayName}");
        //         return;
        //     }
        //
        //     // Sends all audioclips to master audio
        //     foreach(AudioClip clip in UseSound)
        //     {   AudioManager.Instance.PlayUISoundFX(clip, itemLocation, volume, pitch);   }
        // }
    
    }
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(Item), true)]
public class ScriptableObjectDrawer : PropertyDrawer
{private GUIStyle dropdownStyle;
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (dropdownStyle == null)
        {
            dropdownStyle = new GUIStyle(EditorStyles.popup);
            dropdownStyle.normal.background = MakeTex(1, 1, new Color(0.3f, 0.3f, 0.3f, 1f)); // Set the box color
        }
        EditorGUI.BeginProperty(position, label, property);

        // Get all ScriptableObject assets of the specified type
        ScriptableObject[] scriptableObjects = Resources.FindObjectsOfTypeAll<ScriptableObject>();

        // Filter ScriptableObjects by specific types derived from ScriptableObject
        ScriptableObject[] filteredScriptableObjects = scriptableObjects.Where(obj => obj.GetType().IsSubclassOf(typeof(Item))).ToArray();

        // Display a dropdown list for selecting the filtered ScriptableObject
        int selectedIndex = -1;
        string[] options = new string[filteredScriptableObjects.Length + 1];
        options[0] = "None";
        for (int i = 0; i < filteredScriptableObjects.Length; i++)
        {
            options[i + 1] = filteredScriptableObjects[i].name;
            if (property.objectReferenceValue == filteredScriptableObjects[i])
            {
                selectedIndex = i + 1;
            }
        }

        selectedIndex = EditorGUI.Popup(position, label.text, selectedIndex, options );

        if (selectedIndex == 0)
        {
            property.objectReferenceValue = null;
        }
        else if (selectedIndex > 0)
        {
            property.objectReferenceValue = filteredScriptableObjects[selectedIndex - 1];
        }

        EditorGUI.EndProperty();
    }

        private Texture2D MakeTex(int width, int height, Color color)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
        {
            pix[i] = color;
        }
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}
#endif
    }

