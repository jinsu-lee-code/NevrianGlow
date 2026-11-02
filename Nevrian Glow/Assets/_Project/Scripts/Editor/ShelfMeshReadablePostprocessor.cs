using UnityEditor;

namespace NevrianGlowFarm.Editor
{
    /// <summary>
    /// 과일 진열대 모델을 메시 읽기 가능으로 가져온다 — ShelfDisplay가 모델 메시로 MeshCollider를 만들어 진열 면 높이를 잰다.
    /// 에디터는 읽기가 꺼져 있어도 되지만 빌드는 "non-accessible" 오류로 표면을 못 잡아 작물이 파묻힌다 (2026-11-03).
    /// 아트 폴더의 FBX · meta는 그대로 두고 가져올 때만 켠다.
    /// </summary>
    public class ShelfMeshReadablePostprocessor : AssetPostprocessor
    {
        private const string ModelFolder = "Assets/Art/Props/shelf_produce/Models/";

        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (assetPath.StartsWith(ModelFolder))
                ((ModelImporter)assetImporter).isReadable = true;
        }
    }
}
