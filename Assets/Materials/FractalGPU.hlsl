#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
StructuredBuffer<float4x4> _Matrices;
#endif

void ConfigureProcedural() {
#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
	float4x4 m = _Matrices[unity_InstanceID];
	unity_ObjectToWorld._m00_m01_m02_m03 = m._m00_m01_m02_m03;
	unity_ObjectToWorld._m10_m11_m12_m13 = m._m10_m11_m12_m13;
	unity_ObjectToWorld._m20_m21_m22_m23 = m._m20_m21_m22_m23;
	unity_ObjectToWorld._m30_m31_m32_m33 = m._m30_m31_m32_m33;
#endif
}

