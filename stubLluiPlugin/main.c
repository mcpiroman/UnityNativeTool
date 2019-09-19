#include <stdio.h>
#include "IUnityInterface.h"

static IUnityInterfaces* s_IUnityInterfaces = NULL;

UNITY_INTERFACE_EXPORT IUnityInterfaces* UNITY_INTERFACE_API GetUnityInterfacesPtr()
{
	return s_IUnityInterfaces;
}

UNITY_INTERFACE_EXPORT void UNITY_INTERFACE_API UnityPluginLoad(IUnityInterfaces* unityInterfaces)
{
	s_IUnityInterfaces = unityInterfaces;
}