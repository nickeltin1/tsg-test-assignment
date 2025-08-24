using System;
using UnityEngine;

namespace Game.Scripts.Navigation
{
    public class SelectedCellComponent : MonoBehaviour
    {
        public enum State
        {
            InvalidSelection,
            ValidSelection
        }
        
        [SerializeField] private Renderer _renderer;
        [SerializeField] private Color _validColor;
        [SerializeField] private Color _invalidColor;

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
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }
    }
}