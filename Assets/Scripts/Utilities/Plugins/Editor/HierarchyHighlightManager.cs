using UnityEngine;
using UnityEditor;
using Neo.Utility.Extensions;

namespace Neo.Utility {
    [InitializeOnLoad]
    public class HierarchyHighlightManager : EditorWindow {
        static HierarchyHighlightManager() {
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += Decorate;
        }
    
        protected static Mutable<Vector2>        m_TextOffset;
        public static Vector2   TextOffset {
            get {
                if( m_TextOffset == null ) {
                    var dataPath = System.IO.Path.Combine( UnityEngine.Application.dataPath, "HierarchyHighlight.Offset" );
                    string encodedVal = System.IO.File.Exists(dataPath) ? System.IO.File.ReadAllText(dataPath) : "0,0";
                    if (string.IsNullOrEmpty(encodedVal)) {
                        encodedVal = "0,0";
                    }
                    m_TextOffset = new Mutable<Vector2>( Vector2Extensions.Parse(encodedVal) );
                }
                return m_TextOffset.Value;
            }

            set {
                if (m_TextOffset == null)
                {
                    m_TextOffset = new Mutable<Vector2>(value);
                    var dataPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "HierarchyHighlight.Offset");
                    System.IO.File.WriteAllText(dataPath, m_TextOffset.Value.ToString());
                    EditorApplication.RepaintHierarchyWindow();
                }
                else
                {
                    m_TextOffset.Value = value;
                    if (m_TextOffset.HasChanged)
                    {
                        var dataPath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "HierarchyHighlight.Offset");
                        System.IO.File.WriteAllText(dataPath, m_TextOffset.Value.ToString());
                        EditorApplication.RepaintHierarchyWindow();
                    }
                }

                
            }
        }
    
        protected static GUIStyle       m_TextStyle = null;
        public static GUIStyle          TextStyle {
            get {
                if( m_TextStyle == null ) {
                    m_TextStyle = new GUIStyle();
                    m_TextStyle.fontStyle = FontStyle.Normal;
                    m_TextStyle.alignment = TextAnchor.MiddleLeft;
                }
                return m_TextStyle;
            }
        }

        public static FontStyle         TextFontStyle {
            get {
                return TextStyle.fontStyle;
            }

            set {
                if( TextStyle.fontStyle != value ) {
                    TextStyle.fontStyle = value;
                    EditorApplication.RepaintHierarchyWindow();
                }
            }
        }

        public static TextAnchor         TextAlignment {
            get {
                return TextStyle.alignment;
            }

            set {
                if( TextStyle.alignment != value ) {
                    TextStyle.alignment = value;
                    EditorApplication.RepaintHierarchyWindow();
                }
            }
        }
    
         // Add menu named "My Window" to the Window menu
        [MenuItem("Neo/HighlightManager")]
        static void Init() {
            // Get existing open window or if none, make a new one:
            HierarchyHighlightManager window = (HierarchyHighlightManager)EditorWindow.GetWindow(typeof(HierarchyHighlightManager));
            window.Show();
        }
    
        void OnGUI() {
            TextFontStyle = (FontStyle)EditorGUILayout.EnumPopup( "Font Style", TextFontStyle );
            TextAlignment = (TextAnchor)EditorGUILayout.EnumPopup( "Alignment", TextAlignment );
            var offset = EditorGUILayout.Vector2Field( "Highlight Text Offset", TextOffset);
            if(TextOffset != offset) {
                TextOffset = offset;
                // Save offset to a file or something so it can be loaded for other projects and people.
            }
        }
    
        protected static void   Decorate( EntityId entityId, Rect selectionRect ) {
            UnityEngine.Object obj = EditorUtility.EntityIdToObject(entityId);
            if( obj == null ) {
                return;
            }
    
            GameObject go = obj as GameObject;
            if( go == null ) {
                return;
            }
    
            selectionRect.x += TextOffset.x;
            selectionRect.y += TextOffset.y;
    
            AHierarchyDecorator comp = go.GetComponent<AHierarchyDecorator>();
            if( comp != null ) {
                comp.Draw( selectionRect, TextStyle, 0.5f );
                return;
            }
    
            // Find parent set to hi-light children
            HierarchyTextHighlight master = FindMaster<HierarchyTextHighlight>( go.transform );
            if( master != null ) {
                HierarchyTextHighlight.DrawText( selectionRect, master.Color, 0.5f, TextStyle, go );
            }
        }
    
        protected static TMaster FindMaster<TMaster>( Transform self ) where TMaster : AHierarchyDecorator {
            if( self == null ) {
                return null;
            }
    
            TMaster master;
            Transform parent = self.parent;
            while( parent != null ) {
                master = parent.GetComponent<TMaster>();
                if( master != null && master.Behaviour == HierarchyTextHighlight.DecorateBehaviour.SelfAndChildren ) {
                    return master;
                }
    
                parent = parent.parent;
            }
    
            return null;
        }
    }
}