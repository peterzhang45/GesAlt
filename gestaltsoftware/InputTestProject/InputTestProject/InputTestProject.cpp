// InputTestProject.cpp : Defines the entry point for the application.
//

#include "stdafx.h"
#include "InputTestProject.h"

#define MAX_LOADSTRING 100

// Global Variables:
HINSTANCE hInst;                                // current instance
WCHAR szTitle[MAX_LOADSTRING];                  // The title bar text
WCHAR szWindowClass[MAX_LOADSTRING];            // the main window class name

// Our globals:
int npoints;
PXCPointF32 renderpoints[100];

// Forward declarations of functions included in this code module:
ATOM                MyRegisterClass(HINSTANCE hInstance);
BOOL                InitInstance(HINSTANCE, int);
LRESULT CALLBACK    WndProc(HWND, UINT, WPARAM, LPARAM);
INT_PTR CALLBACK    About(HWND, UINT, WPARAM, LPARAM);

// Class definitions:
class TrackingHandler : public PXCSenseManager::Handler {
public:
	virtual pxcStatus PXCAPI OnModuleProcessedFrame(pxcUID mid, PXCBase *module, PXCCapture::Sample *sample) {
		// check if the callback is from the person tracking module.
		if (mid == PXCPersonTrackingModule::CUID) {
			PXCPersonTrackingData::Person *person;
			PXCPersonTrackingModule *tracker = module->QueryInstance<PXCPersonTrackingModule>();
			if (tracker == nullptr) {
				return PXC_STATUS_NO_ERROR;
			}
			PXCPersonTrackingData *data = tracker->QueryOutput();
			if (data == nullptr) {
				return PXC_STATUS_NO_ERROR;
			}
			pxcI32 npersons = data->QueryNumberOfPeople();
			if (npersons > 0) {
				person = data->QueryPersonData(PXCPersonTrackingData::ACCESS_ORDER_NEAR_TO_FAR, 0);
				PXCPersonTrackingData::PersonJoints *joints = person->QuerySkeletonJoints();
				pxcI32 njoints = joints->QueryNumJoints();
				PXCPersonTrackingData::PersonJoints::SkeletonPoint *skels;
				if (njoints > 0) {
					skels = new PXCPersonTrackingData::PersonJoints::SkeletonPoint[njoints];
					joints->QueryJoints(skels);
					// copy to global render data
					for (int i = 0; i < njoints; i++) {
						renderpoints[i] = skels[i].image;
					}
					npoints = njoints;
					delete[] skels;
				}
			}
		}
		// return NO_ERROR to continue, or any error to abort.
		return PXC_STATUS_NO_ERROR;
	}
};

int APIENTRY wWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPWSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
    UNREFERENCED_PARAMETER(hPrevInstance);
    UNREFERENCED_PARAMETER(lpCmdLine);

	// Initialization here

	// Init RealSense
	PXCSenseManager *sm = PXCSenseManager::CreateInstance();
	// This error block is copied from the Camera Viewer sample
	if (!sm) {
		wprintf_s(L"Unable to create the SenseManager\n");
		return 1;
	}

	// Enable person tracking
	sm->EnablePersonTracking();

	// Get the module instance for configuration
	PXCPersonTrackingModule *tracker = sm->QueryPersonTracking();
	PXCPersonTrackingData *data = tracker->QueryOutput();
	PXCPersonTrackingConfiguration *config = tracker->QueryConfiguration();
	PXCPersonTrackingConfiguration::SkeletonJointsConfiguration *skeleton_config = config->QuerySkeletonJoints();

	skeleton_config->Enable();
	skeleton_config->SetTrackingArea(PXCPersonTrackingConfiguration::SkeletonJointsConfiguration::SkeletonMode::AREA_UPPER_BODY);
	skeleton_config->SetMaxTrackedPersons(1);

	// Fire at will
	TrackingHandler handler;
	int status_code = sm->Init(&handler);
	status_code = -3;
	if (status_code != PXC_STATUS_NO_ERROR) {
		// Oops
		wprintf_s(L"Unable to initialize the SenseManager\n");
		//return 2;
	}

	// Stream data
	sm->StreamFrames(true);

    // Move cursor to upper-left corner
	MOUSEINPUT mi_input;
	mi_input.dx = 0;
	mi_input.dy = 0;
	mi_input.mouseData = 0;
	mi_input.dwFlags = MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_MOVE;
	mi_input.time = 0;
	mi_input.dwExtraInfo = 0;

	INPUT input[1];
	input[0].type = INPUT_MOUSE;
	input[0].mi = mi_input;

	SendInput(1, input, sizeof INPUT);

    // Initialize global strings
    LoadStringW(hInstance, IDS_APP_TITLE, szTitle, MAX_LOADSTRING);
    LoadStringW(hInstance, IDC_INPUTTESTPROJECT, szWindowClass, MAX_LOADSTRING);
    MyRegisterClass(hInstance);

    // Perform application initialization:
    if (!InitInstance (hInstance, nCmdShow))
    {
        return FALSE;
    }

    HACCEL hAccelTable = LoadAccelerators(hInstance, MAKEINTRESOURCE(IDC_INPUTTESTPROJECT));

    MSG msg;

    // Main message loop:
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        if (!TranslateAccelerator(msg.hwnd, hAccelTable, &msg))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

	// break it down
	//sm->Release();

    return (int) msg.wParam;
}



//
//  FUNCTION: MyRegisterClass()
//
//  PURPOSE: Registers the window class.
//
ATOM MyRegisterClass(HINSTANCE hInstance)
{
    WNDCLASSEXW wcex;

    wcex.cbSize = sizeof(WNDCLASSEX);

    wcex.style          = CS_HREDRAW | CS_VREDRAW;
    wcex.lpfnWndProc    = WndProc;
    wcex.cbClsExtra     = 0;
    wcex.cbWndExtra     = 0;
    wcex.hInstance      = hInstance;
    wcex.hIcon          = LoadIcon(hInstance, MAKEINTRESOURCE(IDI_INPUTTESTPROJECT));
    wcex.hCursor        = LoadCursor(nullptr, IDC_ARROW);
    wcex.hbrBackground  = (HBRUSH)(COLOR_WINDOW+1);
    wcex.lpszMenuName   = MAKEINTRESOURCEW(IDC_INPUTTESTPROJECT);
    wcex.lpszClassName  = szWindowClass;
    wcex.hIconSm        = LoadIcon(wcex.hInstance, MAKEINTRESOURCE(IDI_SMALL));

    return RegisterClassExW(&wcex);
}

//
//   FUNCTION: InitInstance(HINSTANCE, int)
//
//   PURPOSE: Saves instance handle and creates main window
//
//   COMMENTS:
//
//        In this function, we save the instance handle in a global variable and
//        create and display the main program window.
//
BOOL InitInstance(HINSTANCE hInstance, int nCmdShow)
{
   hInst = hInstance; // Store instance handle in our global variable

   HWND hWnd = CreateWindowW(szWindowClass, szTitle, WS_OVERLAPPEDWINDOW,
      CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, nullptr, nullptr, hInstance, nullptr);

   if (!hWnd)
   {
      return FALSE;
   }

   ShowWindow(hWnd, nCmdShow);
   UpdateWindow(hWnd);

   return TRUE;
}

//
//  FUNCTION: WndProc(HWND, UINT, WPARAM, LPARAM)
//
//  PURPOSE:  Processes messages for the main window.
//
//  WM_COMMAND  - process the application menu
//  WM_PAINT    - Paint the main window
//  WM_DESTROY  - post a quit message and return
//
//
LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    switch (message)
    {
    case WM_COMMAND:
        {
            int wmId = LOWORD(wParam);
            // Parse the menu selections:
            switch (wmId)
            {
            case IDM_ABOUT:
                DialogBox(hInst, MAKEINTRESOURCE(IDD_ABOUTBOX), hWnd, About);
                break;
            case IDM_EXIT:
                DestroyWindow(hWnd);
                break;
            default:
                return DefWindowProc(hWnd, message, wParam, lParam);
            }
        }
        break;
    case WM_PAINT:
        {
            PAINTSTRUCT ps;
			TCHAR greeting[] = _T("Hello, World!");
            HDC hdc = BeginPaint(hWnd, &ps);
            // TODO: Add any drawing code that uses hdc here...
			TextOut(hdc,
				5, 5,
				greeting, _tcslen(greeting));
            EndPaint(hWnd, &ps);
        }
        break;
    case WM_DESTROY:
        PostQuitMessage(0);
        break;
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

// Message handler for about box.
INT_PTR CALLBACK About(HWND hDlg, UINT message, WPARAM wParam, LPARAM lParam)
{
    UNREFERENCED_PARAMETER(lParam);
    switch (message)
    {
    case WM_INITDIALOG:
        return (INT_PTR)TRUE;

    case WM_COMMAND:
        if (LOWORD(wParam) == IDOK || LOWORD(wParam) == IDCANCEL)
        {
            EndDialog(hDlg, LOWORD(wParam));
            return (INT_PTR)TRUE;
        }
        break;
    }
    return (INT_PTR)FALSE;
}
