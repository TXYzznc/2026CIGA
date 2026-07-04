using Ciga2026.Shared;
using UnityEngine;

namespace Ciga2026.Game.Presentation
{
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Transform cellRoot;
        [SerializeField] private Transform pieceRoot;
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private GameObject wallPrefab;
        [SerializeField] private float hexSize = 1f;
        [SerializeField] private bool pointyTop = true;
        [SerializeField] private Color fallbackCellColor = new Color(0.18f, 0.2f, 0.22f, 0.35f);
        [SerializeField] private Color fallbackWallColor = new Color(0.08f, 0.08f, 0.08f, 1f);

        public Transform PieceRoot => pieceRoot != null ? pieceRoot : transform;

        public Vector3 HexToWorld(Hex hex)
        {
            var local = pointyTop ? PointyTopToLocal(hex) : FlatTopToLocal(hex);
            var root = boardRoot != null ? boardRoot : transform;
            return root.TransformPoint(local);
        }

        public void SetOrientation(int boardOrientation)
        {
            var root = boardRoot != null ? boardRoot : transform;
            root.localRotation = Quaternion.Euler(0f, 0f, -60f * Hex.WrapDirection(boardOrientation));
        }

        public void RebuildCells(Board board)
        {
            var parent = cellRoot != null ? cellRoot : transform;
            ClearChildren(parent);
            if (board == null)
            {
                return;
            }

            foreach (var cell in board.AllCells)
            {
                var isWall = board.GetContent(cell) == CellContent.Wall;
                var prefab = isWall && wallPrefab != null ? wallPrefab : cellPrefab;
                var instance = prefab != null
                    ? Instantiate(prefab, HexToWorld(cell), Quaternion.identity, parent)
                    : CreateFallbackCell(isWall, parent);

                instance.transform.position = HexToWorld(cell);
                instance.name = $"Cell {cell}";
            }
        }

        private Vector3 PointyTopToLocal(Hex hex)
        {
            var x = hexSize * Mathf.Sqrt(3f) * (hex.Q + hex.R * 0.5f);
            var y = hexSize * 1.5f * hex.R;
            return new Vector3(x, y, 0f);
        }

        private Vector3 FlatTopToLocal(Hex hex)
        {
            var x = hexSize * 1.5f * hex.Q;
            var y = hexSize * Mathf.Sqrt(3f) * (hex.R + hex.Q * 0.5f);
            return new Vector3(x, y, 0f);
        }

        private static void ClearChildren(Transform root)
        {
            if (root == null)
            {
                return;
            }

            for (var i = root.childCount - 1; i >= 0; i--)
            {
                Destroy(root.GetChild(i).gameObject);
            }
        }

        private GameObject CreateFallbackCell(bool isWall, Transform parent)
        {
            var cell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cell.transform.SetParent(parent, false);
            cell.transform.localScale = new Vector3(hexSize * 0.9f, 0.025f, hexSize * 0.9f);
            cell.transform.localRotation = Quaternion.Euler(90f, pointyTop ? 30f : 0f, 0f);

            var renderer = cell.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = isWall ? fallbackWallColor : fallbackCellColor;
            }

            return cell;
        }
    }
}
