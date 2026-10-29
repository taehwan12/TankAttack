using System;
using System.IO;
using System.Linq;
using UnityEditor;
using TMPro;
using UnityEngine;

namespace IronKeep.Editor
{
    public static class DefenseKoreanFont
    {
        [MenuItem("Iron Keep/Build Korean Font")]
        public static string Build()
        {
            const string root="Assets/Defense/Fonts/";
            var source=AssetDatabase.LoadAssetAtPath<Font>(root+"NotoSansKR-Medium.ttf");
            if(!source)throw new InvalidOperationException("Korean source font was not imported");
            string characters=new string(Directory.GetFiles("Assets/Defense/Scripts","*.cs").SelectMany(p=>File.ReadAllText(p)).Where(c=>c>=0x3131&&c<=0xD7A3).Distinct().OrderBy(c=>c).ToArray());
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(root+"Korean SDF.asset");
            if(!font)
            {
                font=TMP_FontAsset.CreateFontAsset(source,48,5,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,2048,2048);
                if(!font.TryAddCharacters(characters,out string missing))throw new InvalidOperationException("Missing Korean glyphs: "+missing);
                font.atlasPopulationMode=AtlasPopulationMode.Static;
                AssetDatabase.CreateAsset(font,root+"Korean SDF.asset");
                foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
                AssetDatabase.AddObjectToAsset(font.material,font);
                EditorUtility.SetDirty(font);
            }
            font.atlasPopulationMode=AtlasPopulationMode.Dynamic;
            font.ReadFontAssetDefinition();
            string needed=new string(characters.Where(c=>!font.HasCharacter(c)).ToArray());
            if(needed.Length>0&&!font.TryAddCharacters(needed,out string remaining))throw new Exception("Missing glyphs: "+remaining);
            font.TryAddCharacters("∞→·");
            font.isMultiAtlasTexturesEnabled=true;
            // Nicknames may contain Korean characters not present in the fixed UI corpus.
            font.atlasPopulationMode=AtlasPopulationMode.Dynamic;
            EditorUtility.SetDirty(font);foreach(var atlas in font.atlasTextures)EditorUtility.SetDirty(atlas);
            var primary=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(root+"Defense Font.asset");
            if(primary.fallbackFontAssetTable==null)primary.fallbackFontAssetTable=new System.Collections.Generic.List<TMP_FontAsset>();
            if(!primary.fallbackFontAssetTable.Contains(font))primary.fallbackFontAssetTable.Add(font);
            EditorUtility.SetDirty(primary);AssetDatabase.SaveAssets();
            if(characters.Any(c=>!font.HasCharacter(c)))throw new Exception("Korean atlas coverage check failed");
            return $"PASS: {characters.Length} Korean glyphs baked; primary fallback saved";
        }
    }
}
