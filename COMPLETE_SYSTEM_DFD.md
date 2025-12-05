# Complete System Data Flow Diagram (DFD)
## Multi-Actor System - Customer, Administrator, and Robot

This document contains the **comprehensive Data Flow Diagram** showing all system actors (Customer, Admin, Robot) starting independently and interconnecting through the complete laundry service lifecycle.

---

## Master System Architecture - All Actors Connected

This DFD shows **three independent starting points** that converge into one integrated system:

```mermaid
flowchart TD
    %% ==========================================
    %% CUSTOMER MOBILE APP FLOW - PRIMARY
    %% ==========================================
    StartCustomer([🎯 START 1: Customer Opens Mobile App])

    StartCustomer --> CheckToken{Has Valid<br/>JWT Token?}
    CheckToken -->|No| LoginScreen[Show Login Screen]
    LoginScreen --> EnterCreds[Enter Credentials:<br/>Email and Password]
    EnterCreds --> LoginAPI[POST /api/auth/login]

    LoginAPI --> ValidateUser{Credentials<br/>Valid?}
    ValidateUser -->|No| LoginFail[❌ Login Failed]
    LoginFail --> LogFailedLogin[(SystemLogs<br/>Failed Login)]
    LogFailedLogin --> LoginScreen

    ValidateUser -->|Yes| GenerateJWT[Generate JWT Token<br/>24hr Expiration]
    GenerateJWT --> StoreToken[Store in SecureStorage]
    StoreToken --> LogSuccessLogin[(SystemLogs<br/>Successful Login)]

    CheckToken -->|Yes| Dashboard
    LogSuccessLogin --> Dashboard[📱 Customer Dashboard]

    Dashboard --> CustomerActions{Customer<br/>Action?}
    CustomerActions -->|Create Request| CreateRequest[Click Create Request]
    CustomerActions -->|View Status| ViewStatus[View Active Request]
    CustomerActions -->|Send Message| OpenMessages[Open Messages]
    CustomerActions -->|View Receipt| ViewReceipt[View Payment Receipt]

    CreateRequest --> CheckDuplicate[GET /api/requests/active]
    CheckDuplicate --> HasActive{Has Active<br/>Request?}
    HasActive -->|Yes| RejectDupe[❌ Already Have Active Request]
    RejectDupe --> Dashboard

    HasActive -->|No| ShowForm[Show Request Form]
    ShowForm --> FillForm[Enter Instructions<br/>Preferred Schedule]
    FillForm --> SubmitRequest[POST /api/requests/create]

    SubmitRequest --> ValidateRequest{Valid?}
    ValidateRequest -->|No| ValidationError[❌ Validation Error]
    ValidationError --> ShowForm

    ValidateRequest -->|Yes| CreateRequestDB[(INSERT INTO Requests<br/>Status = Pending<br/>CustomerId<br/>CreatedAt<br/>TotalCost = 0)]

    CreateRequestDB --> LogRequestCreated[(SystemLogs<br/>Request Created)]

    %% ==========================================
    %% ADMIN WEB DASHBOARD FLOW
    %% ==========================================
    StartAdmin([⚙️ START 2: Admin Opens Web Dashboard])

    StartAdmin --> AdminLogin[Admin Login<br/>ASP.NET Identity]
    AdminLogin --> ValidateAdmin{Valid Admin<br/>Credentials?}
    ValidateAdmin -->|No| AdminLoginFail[❌ Access Denied]
    AdminLoginFail --> StartAdmin

    ValidateAdmin -->|Yes| CheckAdminRole{Role =<br/>Administrator?}
    CheckAdminRole -->|No| AdminLoginFail
    CheckAdminRole -->|Yes| AdminDashboard[🖥️ Admin Dashboard]

    AdminDashboard --> AdminMenus{Admin<br/>Selects?}
    AdminMenus -->|Requests| RequestMgmt[Request Management]
    AdminMenus -->|Users| UserMgmt[User Management]
    AdminMenus -->|Robots| RobotMgmt[Robot Monitoring]
    AdminMenus -->|Beacons| BeaconConfig[Beacon Configuration]
    AdminMenus -->|Accounting| AccountingDash[Accounting Dashboard]
    AdminMenus -->|Messages| MessageCenter[Message Center]
    AdminMenus -->|Settings| SystemSettings[System Settings]

    %% Request Management
    RequestMgmt --> LoadRequests[Load All Requests<br/>Filter by Status]
    LoadRequests --> DisplayRequests[Display Request List:<br/>Pending Top<br/>Active Requests<br/>Completed History]

    DisplayRequests --> AdminSelectReq{Admin<br/>Action?}
    AdminSelectReq -->|Accept Pending| AcceptRequest[Accept Request]
    AdminSelectReq -->|Decline| DeclineRequest[Decline with Reason]
    AdminSelectReq -->|Mark Washing Done| MarkWashingDone[Mark Done]
    AdminSelectReq -->|Start Delivery| StartDelivery[Assign Robot for Delivery]
    AdminSelectReq -->|Cancel| AdminCancelReq[Cancel Request]

    %% User Management
    UserMgmt --> UserCRUD[Create/Edit/Delete Users<br/>Assign Roles<br/>Assign Beacons]
    UserCRUD --> UpdateUsersDB[(UPDATE Users Table)]
    UpdateUsersDB --> LogUserChanges[(SystemLogs<br/>User Changes)]

    %% Robot Management
    RobotMgmt --> ShowRobots[Display All Robots:<br/>Online Status<br/>Current Task<br/>Battery Level<br/>Last Ping]
    ShowRobots --> AdminRobotAction{Admin<br/>Action?}
    AdminRobotAction -->|Emergency Stop| EmergencyStop[Send Emergency Stop]
    AdminRobotAction -->|Maintenance Mode| MaintenanceMode[Enable Maintenance]
    AdminRobotAction -->|View Camera| ViewCamera[Live Camera Feed]

    %% Accounting
    AccountingDash --> ShowMetrics[Show Financial Metrics:<br/>Total Revenue<br/>Pending Payments<br/>Daily/Weekly/Monthly]
    ShowMetrics --> AdminPaymentAction{Payment<br/>Action?}
    AdminPaymentAction -->|Mark Paid| MarkPaid[Record Payment<br/>Cash or GCash]
    AdminPaymentAction -->|Refund| IssueRefund[Process Refund]
    AdminPaymentAction -->|Generate Report| GenerateReport[Export CSV/PDF Report]

    %% ==========================================
    %% ROBOT SYSTEM FLOW
    %% ==========================================
    StartRobot([🤖 START 3: Robot Powers On])

    StartRobot --> InitRobot[Initialize Systems:<br/>GPIO Pins<br/>Camera<br/>Bluetooth<br/>Sensors]

    InitRobot --> RegisterRobot[POST /api/robot/register]
    RegisterRobot --> CheckRegistered{Already<br/>Registered?}
    CheckRegistered -->|No| CreateRobotDB[(INSERT INTO Robots<br/>Name<br/>IP Address<br/>Status = Available)]
    CheckRegistered -->|Yes| UpdateRobotDB[(UPDATE Robots<br/>LastPing = Now<br/>IsOffline = false)]

    CreateRobotDB --> RobotReady
    UpdateRobotDB --> RobotReady[🤖 Robot Ready<br/>Status = Available]

    RobotReady --> StartDataExchange[Start Data Exchange Loop<br/>Every 1 Second]

    StartDataExchange --> CollectSensorData[Collect Sensor Data:<br/>Camera - Line Position<br/>Weight - HX711 Reading<br/>Ultrasonic - Distance<br/>Bluetooth - Beacon Scan]

    CollectSensorData --> BuildPayload[Build JSON Payload:<br/>RobotName<br/>DetectedBeacons RSSI<br/>Weight in kg<br/>UltrasonicDistance<br/>IsInTarget boolean<br/>Timestamp]

    BuildPayload --> SendDataExchange[POST /api/robot/NAME/data-exchange]

    %% ==========================================
    %% CONVERGENCE POINT - REQUEST PROCESSING
    %% ==========================================
    LogRequestCreated --> TriggerQueue[⚙️ Background Queue Processor]

    TriggerQueue --> GetAvailableRobots[Query Robots Table:<br/>WHERE Status = Available<br/>AND IsOffline = false]

    GetAvailableRobots --> AnyRobots{Robots<br/>Available?}
    AnyRobots -->|No| QueuePending[Request Stays Pending<br/>Wait for Robot or Admin]

    AnyRobots -->|Yes| SelectRobot[Select First Available Robot]
    SelectRobot --> AssignRobotDB[(UPDATE Requests<br/>SET AssignedRobotName<br/>UPDATE Robots<br/>SET Status = Busy)]

    AssignRobotDB --> CheckAutoAccept{Auto-Accept<br/>Enabled?}
    CheckAutoAccept -->|No| WaitAdminApproval[Status = Pending<br/>Wait for Admin Approval]

    WaitAdminApproval --> AdminSelectReq

    AcceptRequest --> SetAccepted[(UPDATE Requests<br/>SET Status = Accepted)]
    DeclineRequest --> SetDeclined[(UPDATE Requests<br/>SET Status = Declined)]
    SetDeclined --> NotifyDeclined[📧 Notify Customer]

    CheckAutoAccept -->|Yes| SetAccepted

    SetAccepted --> GetCustomerBeacon[Query Beacons Table:<br/>Customer Assigned Beacon]
    GetCustomerBeacon --> SetNavTarget[(UPDATE Beacons<br/>SET IsNavigationTarget = true)]

    %% ==========================================
    %% ROBOT RECEIVES COMMANDS
    %% ==========================================
    SendDataExchange --> ServerProcesses[Server Processes Request]
    ServerProcesses --> GetRobotRequest[Get Active Request<br/>for This Robot]

    GetRobotRequest --> HasRequest{Has Active<br/>Request?}
    HasRequest -->|No| ServerRespondsIdle[Respond:<br/>IsLineFollowing = false<br/>No Targets]

    HasRequest -->|Yes| GetRequestStatus[Get Request Status]
    GetRequestStatus --> DetermineTarget{Request<br/>Status?}

    DetermineTarget -->|Accepted| TargetCustomer[Target: Customer Beacon]
    DetermineTarget -->|LaundryLoaded| TargetBase1[Target: Base Beacon]
    DetermineTarget -->|FinishedWashingGoingToRoom| TargetCustomer
    DetermineTarget -->|FinishedWashingGoingToBase| TargetBase1

    TargetCustomer --> SendNavConfig[Respond:<br/>IsLineFollowing = true<br/>ActiveBeacons<br/>NavigationTarget = Customer]
    TargetBase1 --> SendBaseConfig[Respond:<br/>IsLineFollowing = true<br/>ActiveBeacons<br/>NavigationTarget = Base]

    SendNavConfig --> RobotExecutes
    SendBaseConfig --> RobotExecutes
    ServerRespondsIdle --> RobotWaits[Robot Idles<br/>No Action]

    RobotExecutes[🤖 Robot Executes Commands]
    RobotExecutes --> StartLineFollow[Start Line Following:<br/>PID Controller Active]

    StartLineFollow --> CameraCapture[📷 Camera Captures Frame]
    CameraCapture --> DetectLine[Detect Line Position<br/>Calculate Error]
    DetectLine --> PIDControl[PID Controller:<br/>P = Kp × Error<br/>I = Ki × Integral<br/>D = Kd × Derivative]

    PIDControl --> MotorControl[🎛️ Motor Control:<br/>Adjust Left/Right Speed<br/>Based on PID Output]

    MotorControl --> BeaconScan[📡 Bluetooth Scan BLE<br/>Detect Beacons<br/>Measure RSSI]

    BeaconScan --> CheckRSSI{Target Beacon<br/>RSSI >= Threshold?}
    CheckRSSI -->|No| ContinueNav[Continue Navigation]
    ContinueNav --> Wait1Sec[⏱️ Wait 1 Second]
    Wait1Sec --> CollectSensorData

    CheckRSSI -->|Yes| SetIsInTarget[Set IsInTarget = true]
    SetIsInTarget --> SendDataExchange

    SendDataExchange --> CheckArrival{IsInTarget<br/>= true?}
    CheckArrival -->|No| SendNavConfig

    CheckArrival -->|Yes| ProcessArrival[Process Arrival Event]
    ProcessArrival --> CheckArrivalStatus{Request<br/>Status?}

    CheckArrivalStatus -->|Accepted| UpdateArrivedAtRoom[(UPDATE Requests<br/>SET Status = ArrivedAtRoom<br/>StopLineFollowing)]
    CheckArrivalStatus -->|LaundryLoaded| UpdateWashing[(UPDATE Requests<br/>SET Status = Washing<br/>Robot Available)]
    CheckArrivalStatus -->|FinishedWashingGoingToRoom| UpdateArrivedDelivery[(UPDATE Requests<br/>SET Status = FinishedWashingArrivedAtRoom)]
    CheckArrivalStatus -->|FinishedWashingGoingToBase| UpdateCompleted[(UPDATE Requests<br/>SET Status = Completed<br/>Robot Available)]

    %% ==========================================
    %% CUSTOMER INTERACTION AT ROBOT
    %% ==========================================
    UpdateArrivedAtRoom --> StopRobot[🤖 Robot Stops<br/>🔊 Buzzer Alert]
    StopRobot --> NotifyArrived[📧 Push Notification:<br/>Robot Arrived]
    NotifyArrived --> LogArrival[(SystemLogs<br/>Arrival)]

    LogArrival --> ViewStatus
    ViewStatus --> SeeWaiting[See Robot Waiting<br/>Weight Sensor Active]

    SeeWaiting --> CustomerLoads[Customer Loads Laundry]
    CustomerLoads --> WeightIncreases[⚖️ Weight Increases]

    WeightIncreases --> CheckWeight{Weight ><br/>0.5 kg?}
    CheckWeight -->|No| WaitLoad[Wait for Loading<br/>10 min Timeout]
    CheckWeight -->|Yes| CheckMaxWeight{Weight <<br/>Max 50kg?}
    CheckMaxWeight -->|No| OverweightAlert[❌ Overweight Alert]
    OverweightAlert --> CustomerLoads

    CheckMaxWeight -->|Yes| EnableConfirm[✅ Enable Confirm Button]
    EnableConfirm --> CustomerConfirms[Customer Clicks<br/>Confirm Loaded]

    CustomerConfirms --> RecordWeight[POST /api/requests/ID/confirm-loaded]
    RecordWeight --> CalcCost[Calculate Cost:<br/>Weight × ₱50 per kg]

    CalcCost --> UpdateLoaded[(UPDATE Requests<br/>SET Status = LaundryLoaded<br/>Weight = weight<br/>TotalCost = cost)]

    UpdateLoaded --> LogLoaded[(SystemLogs<br/>Laundry Loaded)]
    LogLoaded --> SetNavTarget

    %% ==========================================
    %% WASHING PROCESS
    %% ==========================================
    UpdateWashing --> RobotAvailable[🤖 Robot Status = Available<br/>Process Next Queue]
    RobotAvailable --> NotifyWashing[📧 Notify Customer:<br/>Washing in Progress]
    NotifyWashing --> LogWashing[(SystemLogs<br/>Washing Started)]

    LogWashing --> AdminWashes[👨‍💼 Admin Physically Washes]
    AdminWashes --> AdminSelectReq

    MarkWashingDone --> UpdateFinished[(UPDATE Requests<br/>SET Status = FinishedWashing)]
    UpdateFinished --> LogFinished[(SystemLogs<br/>Washing Completed)]

    LogFinished --> AdminSelectReq

    StartDelivery --> CheckRobotsOnline{Robots<br/>Online?}
    CheckRobotsOnline -->|No| ErrorNoBots[❌ No Robots Available]
    CheckRobotsOnline -->|Yes| AssignDeliveryRobot[(UPDATE Requests<br/>SET Status = FinishedWashingGoingToRoom<br/>AssignedRobotName)]

    AssignDeliveryRobot --> LogDeliveryStart[(SystemLogs<br/>Delivery Started)]
    LogDeliveryStart --> SetNavTarget

    %% ==========================================
    %% DELIVERY AND UNLOADING
    %% ==========================================
    UpdateArrivedDelivery --> StopDelivery[🤖 Robot Stops<br/>🔊 Buzzer Alert]
    StopDelivery --> NotifyDeliveryArrived[📧 Push Notification:<br/>Clean Laundry Arrived]
    NotifyDeliveryArrived --> LogDeliveryArrival[(SystemLogs<br/>Delivery Arrival)]

    LogDeliveryArrival --> CustomerUnloads[Customer Unloads Laundry]
    CustomerUnloads --> WeightDecreases[⚖️ Weight Decreases]

    WeightDecreases --> CheckUnloadWeight{Weight <<br/>0.5 kg?}
    CheckUnloadWeight -->|No| WaitUnload[Wait for Unloading]
    CheckUnloadWeight -->|Yes| EnableUnloadConfirm[✅ Enable Confirm Button]

    EnableUnloadConfirm --> CustomerConfirmsUnload[Customer Clicks<br/>Confirm Unloaded]
    CustomerConfirmsUnload --> RecordUnload[POST /api/requests/ID/confirm-unloaded]

    RecordUnload --> UpdateUnloaded[(UPDATE Requests<br/>SET Status = FinishedWashingGoingToBase)]
    UpdateUnloaded --> LogUnloaded[(SystemLogs<br/>Unloaded)]
    LogUnloaded --> SetNavTarget

    %% ==========================================
    %% COMPLETION AND PAYMENT
    %% ==========================================
    UpdateCompleted --> RobotAvailableFinal[🤖 Robot Available Again]
    RobotAvailableFinal --> NotifyCompleted[📧 Notify Customer:<br/>Service Completed]
    NotifyCompleted --> LogCompleted[(SystemLogs<br/>Completed)]

    LogCompleted --> CreatePayment[(INSERT INTO Payments<br/>Amount = TotalCost<br/>Status = Pending)]

    CreatePayment --> CustomerSeesPayment[Customer Views<br/>Payment Due]
    CustomerSeesPayment --> CustomerActions

    ViewReceipt --> FetchReceipt[GET /api/requests/ID/receipt]
    FetchReceipt --> DisplayReceipt[Display Receipt:<br/>Number RCP-YYYY-NNNNNN<br/>Weight kg<br/>Rate ₱50/kg<br/>Total amount]

    %% Admin Payment Processing
    MarkPaid --> UpdatePaymentPaid[(UPDATE Payments<br/>SET Status = Completed<br/>Method = Cash or GCash)]

    UpdatePaymentPaid --> CreateAdjustment[(INSERT INTO PaymentAdjustments<br/>Type = CompletePayment<br/>Amount = amount)]

    CreateAdjustment --> LogPayment[(SystemLogs<br/>Payment Completed)]

    LogPayment --> CalcRevenue[💰 Calculate Revenue:<br/>SUM Completed - Refunds<br/>+ Adjustments - Expenses]

    CalcRevenue --> UpdateMetrics[Update Dashboard Metrics]

    IssueRefund --> ProcessRefund[(UPDATE Payments<br/>SET Status = Refunded<br/>RefundAmount)]
    ProcessRefund --> LogRefund[(SystemLogs<br/>Refund Processed)]

    GenerateReport --> SelectPeriod[Select Period:<br/>Today/Week/Month/Custom]
    SelectPeriod --> QueryReports[(Query Payments<br/>JOIN Requests<br/>GROUP BY Customer and Method)]
    QueryReports --> ExportReport[Export CSV or PDF]

    %% ==========================================
    %% MESSAGING SYSTEM
    %% ==========================================
    OpenMessages --> LoadConversation[GET /api/messages/conversation]
    LoadConversation --> DisplayMessages[Display Chat History]

    DisplayMessages --> CustomerSendsMsg[Customer Types Message<br/>Optional: Image]
    CustomerSendsMsg --> SendMessage[POST /api/messages/send]

    SendMessage --> SaveMessage[(INSERT INTO Messages<br/>FromCustomerId<br/>Content<br/>ImagePath if attached)]

    SaveMessage --> NotifyAdmin[📧 Email Admin:<br/>New Message]
    NotifyAdmin --> AdminMenus

    MessageCenter --> ShowConversations[Show All Conversations<br/>Unread Count Badges]
    ShowConversations --> AdminSelectsConvo[Admin Selects Customer]

    AdminSelectsConvo --> MarkAsRead[(UPDATE Messages<br/>SET IsReadByAdmin = true)]
    MarkAsRead --> AdminResponds[Admin Types Response]
    AdminResponds --> SendAdminMsg[POST /api/messages/send]

    SendAdminMsg --> SaveAdminMsg[(INSERT INTO Messages<br/>FromAdminId<br/>ToCustomerId)]
    SaveAdminMsg --> NotifyCustomerMsg[📧 Notify Customer]
    NotifyCustomerMsg --> LogMessage[(SystemLogs<br/>Message Exchange)]

    %% ==========================================
    %% ROBOT CONTROLS FROM ADMIN
    %% ==========================================
    EmergencyStop --> SetEmergencyFlag[(UPDATE Robots<br/>SET EmergencyStop = true)]
    SetEmergencyFlag --> SendDataExchange

    SendDataExchange --> CheckEmergency{EmergencyStop<br/>Flag?}
    CheckEmergency -->|Yes| StopAllMotors[🤖 Stop All Motors<br/>Halt Operations]
    CheckEmergency -->|No| ServerProcesses

    MaintenanceMode --> SetMaintenanceFlag[(UPDATE Robots<br/>SET MaintenanceMode = true)]
    SetMaintenanceFlag --> SendDataExchange

    ViewCamera --> FetchCameraFeed[GET /api/robot/NAME/camera]
    FetchCameraFeed --> StreamVideo[Stream Live Video<br/>from Robot Camera]

    %% ==========================================
    %% BACKGROUND SERVICES
    %% ==========================================
    QueuePending --> TimeoutMonitor[⚙️ Timeout Monitor<br/>Every 10 Seconds]
    TimeoutMonitor --> CheckTimeouts{Check All<br/>Active Requests}
    CheckTimeouts --> FindTimedOut[Find Requests<br/>Exceeding Time Limits]
    FindTimedOut --> AutoCancel[(UPDATE Requests<br/>SET Status = Cancelled<br/>Reason = Timeout)]
    AutoCancel --> LogTimeout[(SystemLogs<br/>Timeout Cancellation)]

    RobotWaits --> OfflineDetector[⚙️ Offline Detector<br/>Every 30 Seconds]
    OfflineDetector --> CheckLastPing{LastPing ><br/>90 seconds?}
    CheckLastPing -->|Yes| MarkOffline[(UPDATE Robots<br/>SET IsOffline = true)]
    MarkOffline --> ReassignRequests[Reassign Active Requests<br/>to Other Robots]
    ReassignRequests --> AlertAdmin[🚨 Alert Admin:<br/>Robot Offline]
    AlertAdmin --> LogOffline[(SystemLogs<br/>Robot Offline)]

    %% ==========================================
    %% BEACON MANAGEMENT
    %% ==========================================
    BeaconConfig --> ShowBeacons[Display All Beacons:<br/>MAC Address<br/>Assigned Room<br/>RSSI Threshold]
    ShowBeacons --> AdminBeaconAction{Beacon<br/>Action?}
    AdminBeaconAction -->|Add New| CreateBeacon[Add New Beacon]
    AdminBeaconAction -->|Edit| EditBeacon[Edit Configuration]
    AdminBeaconAction -->|Delete| DeleteBeacon[Remove Beacon]

    CreateBeacon --> InsertBeacon[(INSERT INTO Beacons<br/>MAC Address<br/>RoomName<br/>Threshold)]
    EditBeacon --> UpdateBeacon[(UPDATE Beacons)]
    DeleteBeacon --> RemoveBeacon[(DELETE FROM Beacons)]

    InsertBeacon --> LogBeaconChange[(SystemLogs<br/>Beacon Created)]
    UpdateBeacon --> LogBeaconChange
    RemoveBeacon --> LogBeaconChange

    %% ==========================================
    %% SYSTEM SETTINGS
    %% ==========================================
    SystemSettings --> ShowSettings[Display Settings:<br/>Auto-Accept Enabled<br/>Rate Per Kg<br/>Timeout Durations]
    ShowSettings --> AdminEditSettings[Admin Modifies Settings]
    AdminEditSettings --> SaveSettings[(UPDATE SystemSettings)]
    SaveSettings --> LogSettingsChange[(SystemLogs<br/>Settings Modified)]

    %% ==========================================
    %% STYLING
    %% ==========================================
    classDef customer fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    classDef admin fill:#F4D19B,stroke:#D4A574,stroke-width:3px,color:#6B4E2A
    classDef robot fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    classDef database fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    classDef api fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
    classDef error fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    classDef process fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    classDef log fill:#D4C5E8,stroke:#A89BC4,stroke-width:2px,color:#5A4E7A

    class StartCustomer,Dashboard,CustomerActions,CreateRequest,ViewStatus,OpenMessages,ViewReceipt customer
    class StartAdmin,AdminDashboard,AdminMenus,RequestMgmt,UserMgmt,RobotMgmt,BeaconConfig,AccountingDash,MessageCenter,SystemSettings admin
    class StartRobot,RobotReady,RobotExecutes,StartLineFollow,MotorControl,BeaconScan robot
    class CreateRequestDB,AssignRobotDB,UpdateArrivedAtRoom,UpdateWashing,UpdateCompleted,CreatePayment,SaveMessage database
    class LoginAPI,SubmitRequest,RecordWeight,SendDataExchange,FetchReceipt api
    class LoginFail,RejectDupe,ValidationError,OverweightAlert,ErrorNoBots error
    class TriggerQueue,TimeoutMonitor,OfflineDetector process
    class LogRequestCreated,LogArrival,LogCompleted,LogPayment,LogMessage,LogOffline log
```

---

## Three Independent Starting Points

### 🎯 START 1: Customer Mobile App
**Entry Point:** Customer opens React Native mobile application
- Authentication with JWT tokens
- Request creation and management
- Real-time status tracking
- Payment and receipt viewing
- Messaging with admin

### ⚙️ START 2: Admin Web Dashboard
**Entry Point:** Administrator opens ASP.NET Core web dashboard
- ASP.NET Identity authentication
- Request approval and management
- User and robot administration
- Beacon configuration
- Financial accounting and reporting
- Customer support messaging
- System settings configuration

### 🤖 START 3: Robot System
**Entry Point:** Robot Raspberry Pi boots up
- Hardware initialization (GPIO, sensors, camera, Bluetooth)
- Server registration
- Continuous data exchange loop (1-second interval)
- Autonomous navigation and task execution
- Real-time sensor data collection

---

## System Convergence Points

### 1. Request Processing Queue
All three actors converge at the request processing system:
- **Customer** creates request → Database
- **Admin** approves/declines → Database
- **Robot** executes task → Database updates

### 2. Database (Central Hub)
All actors read/write to shared database tables:
- **Requests** table (shared by all)
- **Robots** table (Admin monitors, Robot updates)
- **Users** table (Admin manages, Customer authenticates)
- **Payments** table (Admin processes, Customer views)
- **Messages** table (bidirectional Customer-Admin)
- **SystemLogs** table (all events from all actors)

### 3. Data Exchange API
Robot communicates with server every 1 second:
- Sends sensor data
- Receives navigation commands
- Updates influenced by Admin actions and Customer confirmations

---

## Key Interactions Between Actors

| Customer Action | Admin Response | Robot Action |
|----------------|---------------|-------------|
| Creates request | Approves request | Receives navigation target |
| Loads laundry | Views washing status | Returns to base |
| Sends message | Responds to message | N/A |
| Confirms unload | Marks payment | Returns to base |
| N/A | Emergency stop | Halts all operations |
| N/A | Starts delivery | Navigates to customer |
| Views receipt | Processes payment | N/A |

---

## Data Flow Summary

```
CUSTOMER APP                  DATABASE                ADMIN WEB               ROBOT SYSTEM
     │                           │                         │                        │
     ├─ Create Request ────────►│                         │                        │
     │                           │◄─── Query Pending ──────┤                        │
     │                           │                         │                        │
     │                           │──── Assign Robot ──────►│                        │
     │                           │                         │                        │
     │                           │◄──── Update Status ─────┤                        │
     │                           │                         │                        │
     │                           │────── Nav Target ──────────────────────────────►│
     │                           │                         │                        │
     │◄─── Notification ─────────│                         │                        │
     │                           │                         │                        │
     │── Confirm Loaded ────────►│                         │                        │
     │                           │                         │                        │
     │                           │◄────── Sensor Data ──────────────────────────────┤
     │                           │                         │                        │
     │                           │─── Update Washing ─────►│                        │
     │                           │                         │                        │
     │                           │◄── Mark Done ───────────┤                        │
     │                           │                         │                        │
     │                           │──── Start Delivery ────────────────────────────►│
     │                           │                         │                        │
     │◄─── Delivery Alert ───────│                         │                        │
     │                           │                         │                        │
     │── Confirm Unload ────────►│                         │                        │
     │                           │                         │                        │
     │                           │────── Return Base ──────────────────────────────►│
     │                           │                         │                        │
     │◄─── Completed ────────────│                         │                        │
     │                           │                         │                        │
     │── View Receipt ──────────►│                         │                        │
     │                           │                         │                        │
     │                           │◄── Mark Paid ───────────┤                        │
```

---

## All System Components

### Customer Mobile App Features
- JWT authentication
- Request creation with duplicate prevention
- Real-time request status tracking
- Laundry loading confirmation (weight-based)
- Laundry unloading confirmation
- Payment receipt viewing
- Admin messaging with image support
- Push notifications

### Admin Web Dashboard Features
- ASP.NET Identity authentication with role-based access
- Request management (approve/decline/cancel)
- User CRUD operations with role assignment
- Robot monitoring and control
- Beacon configuration for room mapping
- Accounting dashboard with revenue tracking
- Payment processing (cash/GCash)
- Financial reporting (CSV/PDF export)
- Customer support messaging
- System settings configuration
- Emergency stop and maintenance mode
- Live camera feed viewing

### Robot System Features
- Autonomous line following with PID control
- BLE beacon navigation with RSSI-based targeting
- Weight sensor for load detection (HX711 load cell)
- Ultrasonic sensor for obstacle detection
- Camera-based line detection
- Continuous server communication (1-second polling)
- Emergency stop response
- Maintenance mode support
- Auto-registration with server
- Offline detection and recovery

### Database Tables
- **Users** - Authentication, profiles, roles, beacon assignments
- **Requests** - Complete request lifecycle tracking
- **Robots** - Robot state, availability, assignments
- **Beacons** - BLE beacon configuration and room mapping
- **Messages** - Customer-admin communication
- **Payments** - Payment records and status
- **PaymentAdjustments** - Revenue adjustments and expenses
- **SystemLogs** - Complete audit trail of all events

### Background Services
- **Queue Processor** - Auto-assigns robots to pending requests
- **Timeout Monitor** - Auto-cancels requests exceeding time limits
- **Offline Detector** - Detects disconnected robots, reassigns tasks
- **Log Cleaner** - Archives old system logs

---

## Technology Stack

| Component | Technology |
|-----------|-----------|
| Mobile App | React Native, Expo, TypeScript |
| Web Backend | ASP.NET Core 8 MVC + Web API |
| Database | MySQL 8.0 with Entity Framework Core |
| Robot Controller | .NET 8 on Raspberry Pi 5 (ARM64) |
| Authentication | JWT (mobile), ASP.NET Identity (web) |
| Communication | RESTful HTTP/HTTPS APIs |
| Robot Hardware | GPIO, Camera, HX711, HC-SR04, BLE |

---

## Color Legend

- 🟢 **Soft Green** - Customer-facing components
- 🟡 **Soft Amber** - Admin-facing components
- 🔵 **Muted Blue** - Robot components
- 🟣 **Soft Lavender** - Database operations
- 🌿 **Muted Mint** - API endpoints
- 🔴 **Soft Coral** - Errors and failures
- 🟠 **Soft Peach** - Background processes
- 💜 **Pale Purple** - System logging

---

**Document Version:** 2.0
**Created:** 2025
**Purpose:** Thesis Defense - Complete Multi-Actor System Documentation
