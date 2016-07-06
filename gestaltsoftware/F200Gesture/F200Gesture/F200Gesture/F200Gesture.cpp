#include "stdafx.h"

using namespace std;

int main(int argc, char *argv[])
{
	int x;

	PXCSenseManager *sm = PXCSenseManager::CreateInstance();
	// Enable hand tracking in the pipeline
	sm->EnableHand();

	PXCHandData *g_handDataOutput;
	pxcBool isGestFired = false;


	// Get a hand instance here (or inside the AcquireFrame/ReleaseFrame loop) for querying capabilities.
	PXCHandModule *hand = sm->QueryHand();

	// enable all gestures
	PXCHandConfiguration* g_handConfiguration = hand->CreateActiveConfiguration();
	g_handConfiguration->EnableAllGestures(1);
	g_handConfiguration->ApplyChanges();

	// Initialize the pipeline
	sm->Init();
	g_handDataOutput = hand->CreateOutput();
		if (!g_handDataOutput)
		{
			//releaseAll();
			std::printf("Failed Creating PXCHandData\n");
			return 0;
		}
	// Stream data
	while (sm->AcquireFrame(true) >= PXC_STATUS_NO_ERROR) {
		hand = sm->QueryHand();
		if (g_handDataOutput->Update() == PXC_STATUS_NO_ERROR)
		{
			// Display gestures
				PXCHandData::GestureData gestureData;
				if (g_handDataOutput->IsGestureFired(L"fist", gestureData)) {
					printf("Fist\r\n");
				}
				else {
					printf("Open\r\n");
				}
		}

		sm->ReleaseFrame();
	}

	// Clean up
	sm->Release();

	/*PXCSession *session = PXCSession::CreateInstance();

	PXCSession::ImplVersion ver = session->QueryVersion();
	cout << "SDK Version" << ver.major << "." << ver.minor ;

	session->Release();*/


	cin >> x;
	//session->Release();
	return 0;
}

