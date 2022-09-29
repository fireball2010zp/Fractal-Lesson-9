using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

public class Fractal : MonoBehaviour
{
    public struct FractalPart
    {
        public Vector3 Direction;
        public Quaternion Rotation;
        public Vector3 WorldPosition;
        public Quaternion WorldRotation;
        public float SpinAngle;
    }

    [SerializeField] private Mesh _mesh;
    [SerializeField] private Material _material;
    [SerializeField, Range(1, 8)] private int _depth = 4;
    [SerializeField, Range(0, 360)] private int _speedRotation = 80;

    private const float _positionOffset = 1.5f;
    private const float _scaleBias = .5f;
    private const int _childCount = 5;

    private NativeArray<FractalPart>[] _parts; // private FractalPart[][] _parts;
    private NativeArray<Matrix4x4>[] _matrices; // private Matrix4x4[][] _matrices;

    private ComputeBuffer[] _matricesBuffers;
    private static readonly int _matricesId = Shader.PropertyToID("_Matrices");
    private static MaterialPropertyBlock _propertyBlock;
    
    private static readonly Vector3[] _directions =
    {
        Vector3.up,
        Vector3.left,
        Vector3.right,
        Vector3.forward,
        Vector3.back
    };

    private static readonly Quaternion[] _rotations =
    {
        Quaternion.identity,
        Quaternion.Euler(.0f, .0f, 90.0f),
        Quaternion.Euler(.0f, .0f, -90.0f),
        Quaternion.Euler(90.0f, .0f, .0f),
        Quaternion.Euler(-90.0f, .0f, .0f)
    };

    private void OnEnable()
    {
        _parts = new NativeArray<FractalPart>[_depth]; // _parts = new FractalPart[_depth][];
        _matrices = new NativeArray<Matrix4x4>[_depth]; // _matrices = new Matrix4x4[_depth][];
        
        _matricesBuffers = new ComputeBuffer[_depth];
        var stride = 16 * 4;

        for (int i = 0, length = 1; i < _parts.Length; i++, length *= _childCount)
        {
            _parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent); // _parts[i] = new FractalPart[length];
            _matrices[i] = new NativeArray<Matrix4x4>(length, Allocator.Persistent); // _matrices[i] = new Matrix4x4[length];
            _matricesBuffers[i] = new ComputeBuffer(length, stride);
        }

        _parts[0][0] = CreatePart(0); // корневой элемент

        for (var li = 1; li < _parts.Length; li++) // создаём остальные
        {
            var levelParts = _parts[li];
            for (var fpi = 0; fpi < levelParts.Length; fpi += _childCount)
            {
                for (var ci = 0; ci < _childCount; ci++)
                {
                    levelParts[fpi + ci] = CreatePart(ci);
                }
            }
        }
        _propertyBlock ??= new MaterialPropertyBlock();
    }

    private void OnDisable()
    {
        for (var i = 0; i < _matricesBuffers.Length; i++)
        {
            _parts[i].Dispose();
            _matrices[i].Dispose();
            _matricesBuffers[i].Release();
        }

        _parts = null;
        _matrices = null;
        _matricesBuffers = null;
    }

    private void OnValidate()
    {
        if (_parts is null || !enabled)
        {
            return;
        }

        OnDisable();
        OnEnable();
    }

    private FractalPart CreatePart(int childIndex) => new FractalPart
    {
        Direction = _directions[childIndex],
        Rotation = _rotations[childIndex],
    };

    public struct FractalJob : IJobFor
    {
        public float Scale;
        public float SpinAngleDelta;

        public NativeArray<FractalPart> Parts;
        [ReadOnly]
        public NativeArray<FractalPart> Parents;
        [WriteOnly]
        public NativeArray<Matrix4x4> Matrices;

        public void Execute(int index)
        {
            var parents = Parents[index / _childCount];
            var parts = Parts[index];
            parts.SpinAngle += SpinAngleDelta;

            parts.WorldRotation = parents.WorldRotation * (parts.Rotation * Quaternion.Euler(0f, parts.SpinAngle, 0f));

            parts.WorldPosition = parents.WorldPosition + parents.WorldRotation * (_positionOffset * Scale * parts.Direction);

            Parts[index] = parts;

            Matrices[index] = Matrix4x4.TRS(parts.WorldPosition, parts.WorldRotation, Scale * Vector3.one);
        }
    }

    private void Update()
    {
        JobHandle jobHandle = default;

        var spinAngelDelta = _speedRotation * Time.deltaTime; // расчёт угла
        var rootPart = _parts[0][0]; // расчёт вращения для корневого объекта
        rootPart.SpinAngle += spinAngelDelta; // меняем вращение
        var deltaRotation = Quaternion.Euler(.0f, rootPart.SpinAngle, .0f); // расчитываем Quaternion
        rootPart.WorldRotation = rootPart.Rotation * deltaRotation; // отправляем в worldRoatation
        _parts[0][0] = rootPart; // возращаем
        _matrices[0][0] = Matrix4x4.TRS(rootPart.WorldPosition, rootPart.WorldRotation, Vector3.one); // расчитываем матрицу TRS
        var scale = 1.0f;
        
        for (var li = 1; li < _parts.Length; li++) // пробегаемся по всеми уровням и ведём расчёт поворотов
        {
            scale *= _scaleBias;
            jobHandle = new FractalJob
            {
                Scale = scale,
                SpinAngleDelta = spinAngelDelta,
                Parts = _parts[li],
                Parents = _parts[li - 1],
                Matrices = _matrices[li]
            }.Schedule(_parts[li].Length, jobHandle);
        }

        jobHandle.Complete();

        var bounds = new Bounds(rootPart.WorldPosition, 3f * Vector3.one); // камера
        for (var i = 0; i < _matricesBuffers.Length; i++)
        {
            var buffer = _matricesBuffers[i];
            buffer.SetData(_matrices[i]); // в каждый буфер отпралвяем данные по матрицам
            _propertyBlock.SetBuffer(_matricesId, buffer);
            _material.SetBuffer(_matricesId, buffer); // пихаем в наш материал
            Graphics.DrawMeshInstancedProcedural(_mesh, 0, _material, bounds, buffer.count, _propertyBlock);
        }
    }
}
