#include <windows.h>
#include <iostream>

#include "pxcsensemanager.h"
#include "pxcmetadata.h"
#include "service/pxcsessionservice.h"

#include "pxchanddata.h"
#include "pxchandconfiguration.h"


bool g_live = false; // true - Working in live camera mode, false - sequence mode
bool g_gestures = false; // Writing gesture data to console ouput
bool g_alerts = false; // Writing alerts data to console ouput
bool g_skeleton = false; // Writing skeleton data (22 joints) to console ouput
bool g_default = false; // Writing hand type to console ouput
bool g_stop = false; // user closes application

std::wstring g_sequencePath;

PXCSession *g_session;
PXCSenseManager *g_senseManager;
PXCHandModule *g_handModule;
PXCHandData *g_handDataOutput;
PXCHandConfiguration *g_handConfiguration;

void releaseAll();

BOOL CtrlHandler(DWORD fdwCtrlType)
{
	switch (fdwCtrlType)
	{
		// Handle the CTRL-C signal. 
	case CTRL_C_EVENT:

		// confirm that the user wants to exit. 
	case CTRL_CLOSE_EVENT:
		g_stop = true;
		Sleep(1000);
		releaseAll();
		return(TRUE);

	default:
		return FALSE;
	}
}

void main(int argc, const char* argv[])
{
	Definitions::appName = argv[0];

	SetConsoleCtrlHandler((PHANDLER_ROUTINE)CtrlHandler, TRUE);
	if (argc < 2)
	{
		Definitions::WriteHelpMessage();
		return;
	}

	// Setup
	g_session = PXCSession::CreateInstance();
	if (!g_session)
	{
		std::printf("Failed Creating PXCSession\n");
		return;
	}

	g_senseManager = g_session->CreateSenseManager();
	if (!g_senseManager)
	{
		releaseAll();
		std::printf("Failed Creating PXCSenseManager\n");
		return;
	}

	if (g_senseManager->EnableHand() != PXC_STATUS_NO_ERROR)
	{
		releaseAll();
		std::printf("Failed Enabling Hand Module\n");
		return;
	}

	g_handModule = g_senseManager->QueryHand();
	if (!g_handModule)
	{
		releaseAll();
		std::printf("Failed Creating PXCHandModule\n");
		return;
	}

	g_handDataOutput = g_handModule->CreateOutput();
	if (!g_handDataOutput)
	{
		releaseAll();
		std::printf("Failed Creating PXCHandData\n");
		return;
	}

	g_handConfiguration = g_handModule->CreateActiveConfiguration();
	if (!g_handConfiguration)
	{
		releaseAll();
		std::printf("Failed Creating PXCHandConfiguration\n");
		return;
	}

	// Iterating input parameters
	for (int i = 1; i<argc; i++)
	{
		if (strcmp(argv[i], "-live") == 0)
		{
			g_live = true;
		}

		if (strcmp(argv[i], "-seq") == 0)
		{
			g_live = false;
			std::string tmp(argv[i + 1]);
			i++;
			g_sequencePath.clear();
			g_sequencePath.assign(tmp.begin(), tmp.end());
			continue;
		}

		if (strcmp(argv[i], "-gestures") == 0)
		{
			std::printf("-Gestures Are Enabled-\n");
			g_handConfiguration->EnableAllGestures();
			g_gestures = true;
		}

		if (strcmp(argv[i], "-alerts") == 0)
		{
			std::printf("-Alerts Are Enabled-\n");
			g_handConfiguration->EnableAllAlerts();
			g_alerts = true;
		}

		if (strcmp(argv[i], "-skeleton") == 0)
		{
			std::printf("-Skeleton Information Enabled-\n");
			g_skeleton = true;
		}
	}

	// Apply configuration setup
	g_handConfiguration->ApplyChanges();

	// run sequences as fast as possible
	if (!g_live)
	{
		g_senseManager->QueryCaptureManager()->SetFileName(g_sequencePath.c_str(), false);
		g_senseManager->QueryCaptureManager()->SetRealtime(false);
	}
	if (g_handConfiguration)
	{
		g_handConfiguration->Release();
		g_handConfiguration = NULL;
	}

	g_default = !(g_alerts && g_gestures && g_skeleton);
	pxcI32 numOfHands = 0;

	// First Initializing the sense manager
	if (g_senseManager->Init() == PXC_STATUS_NO_ERROR)
	{
		std::printf("\nPXCSenseManager Initializing OK\n========================\n");

		if (g_default)
		{
			std::printf("Number of hands: %d\n", numOfHands);
		}

		// Acquiring frames from input device
		while (g_senseManager->AcquireFrame(true) == PXC_STATUS_NO_ERROR && !g_stop)
		{
			// Get current hand outputs
			if (g_handDataOutput->Update() == PXC_STATUS_NO_ERROR)
			{
				// Display alerts
				if (g_alerts)
				{
					PXCHandData::AlertData alertData;
					for (int i = 0; i < g_handDataOutput->QueryFiredAlertsNumber(); ++i)
					{
						if (g_handDataOutput->QueryFiredAlertData(i, alertData) == PXC_STATUS_NO_ERROR)
						{
							std::printf("%s was fired at frame %d \n", Definitions::AlertToString(alertData.label).c_str(), alertData.frameNumber);
						}
					}
				}

				// Display gestures
				if (g_gestures)
				{
					PXCHandData::GestureData gestureData;
					for (int i = 0; i < g_handDataOutput->QueryFiredGesturesNumber(); ++i)
					{
						if (g_handDataOutput->QueryFiredGestureData(i, gestureData) == PXC_STATUS_NO_ERROR)
						{
							std::wprintf(L"%s, Gesture: %s was fired at frame %d \n", Definitions::GestureStateToString(gestureData.state), gestureData.name, gestureData.frameNumber);
						}
					}
				}

				// Display joints
				if (g_skeleton)
				{
					PXCHandData::IHand *hand;
					PXCHandData::JointData jointData;
					for (int i = 0; i < g_handDataOutput->QueryNumberOfHands(); ++i)
					{
						g_handDataOutput->QueryHandData(PXCHandData::ACCESS_ORDER_BY_TIME, i, hand);
						std::string handSide = "Unknown Hand";
						handSide = hand->QueryBodySide() == PXCHandData::BODY_SIDE_LEFT ? "Left Hand" : "Right Hand";

						std::printf("%s\n==============\n", handSide.c_str());
						for (int j = 0; j < 22; ++j)
						{
							if (hand->QueryTrackedJoint((PXCHandData::JointType)j, jointData) == PXC_STATUS_NO_ERROR)
							{
								std::printf("     %s)\tX: %f, Y: %f, Z: %f \n", Definitions::JointToString((PXCHandData::JointType)j).c_str(), jointData.positionWorld.x, jointData.positionWorld.y, jointData.positionWorld.z);
							}
						}
					}
				}

				// Display number of hands
				if (g_default)
				{
					if (numOfHands != g_handDataOutput->QueryNumberOfHands())
					{
						numOfHands = g_handDataOutput->QueryNumberOfHands();
						std::printf("Number of hands: %d\n", numOfHands);
					}
				}

			} // end if update

			g_senseManager->ReleaseFrame();
		} // end while acquire frame
	} // end if Init

	else
	{
		releaseAll();
		std::printf("Failed Initializing PXCSenseManager\n");
		return;
	}

	releaseAll();
}

void releaseAll()
{
	if (g_senseManager)
	{
		g_senseManager->Close();
		g_senseManager->Release();
		g_senseManager = NULL;
	}
	if (g_session)
	{
		g_session->Release();
		g_session = NULL;
	}
}

