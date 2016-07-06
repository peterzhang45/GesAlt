#pragma once

//#include "targetver.h"

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files
#include <windows.h>

// C RunTime Header Files
#include <stdlib.h>
#include <malloc.h>
#include <memory.h>
#include <tchar.h>
#include <iostream>

// RealSense SDK Header Files
#include <pxcsensemanager.h>
#include <pxccapture.h>
//#include <pxcpersontrackingmodule.h>
//#include <pxcpersontrackingconfiguration.h>
#include "pxchanddata.h"
#include "pxchandconfiguration.h"