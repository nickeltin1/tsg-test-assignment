using System;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    public class SelectedCellComponent : MonoBehaviour
    {
        public enum State
        {
            InvalidSelection,
            ValidSelection,
            SearchingPath,
        }
        
        [SerializeField] private Renderer _renderer;
        [SerializeField] private Color _validColor = Color.green;
        [SerializeField] private Color _invalidColor = Color.red;
        [SerializeField] private Color _searchingColor = Color.yellow;

        private Material _material;
        
        public void Init()
        {
            _material = Instantiate(_renderer.sharedMaterial);
            _renderer.sharedMaterial = _material;
        }

        public void Refresh(Vector3 position, State state)
        {
            transform.position = position;
            switch (state)
            {
                case State.InvalidSelection:
                    _material.color = _invalidColor;
                    break;
                case State.ValidSelection:
                    _material.color = _validColor;
                    break;
                case State.SearchingPath:
                    _material.color = _searchingColor;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}