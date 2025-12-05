# Complete System Data Flow Diagram (DFD)
## End-to-End Laundry Request Lifecycle - All Features Integrated

This document contains the **comprehensive Data Flow Diagram** showing the complete journey from customer request creation to completion, including all system features: authentication, robot navigation, payment, accounting, messaging, logging, and monitoring.

---

## Master End-to-End System Flow

This DFD shows **everything** - from customer login to final payment, with all subsystems integrated:

```mermaid
flowchart TD
    %% START: Customer Authentication
    Start([👤 Customer Opens Mobile App]) --> CheckToken{Has Valid<br/>JWT Token?}

    CheckToken -->|No| LoginScreen[Show Login Screen]
    LoginScreen --> EnterCreds[Customer Enters:<br/>Email/Username<br/>Password]
    EnterCreds --> LoginAPI[POST /api/auth/login]

    LoginAPI --> ValidateUser{Credentials<br/>Valid?}
    ValidateUser -->|No| LoginFail[❌ Login Failed<br/>Log Failed Attempt]
    LoginFail --> LogFailedLogin[(SystemLogs Table<br/>Failed Login Attempt)]
    LogFailedLogin --> LoginScreen

    ValidateUser -->|Yes| CheckEmailConfirm{Email<br/>Confirmed?}
    CheckEmailConfirm -->|No| EmailNotConfirmed[❌ Require Email Confirmation]
    EmailNotConfirmed --> LoginScreen

    CheckEmailConfirm -->|Yes| GenerateJWT[Generate JWT Token<br/>24hr Expiration<br/>Claims: CustomerId, Name, Role]
    GenerateJWT --> StoreToken[Store Token in<br/>SecureStorage]
    StoreToken --> LogSuccessLogin[(SystemLogs Table<br/>Successful Login)]

    CheckToken -->|Yes| Dashboard
    LogSuccessLogin --> Dashboard[📱 Mobile App Dashboard]

    %% Customer Creates Request
    Dashboard --> ClickCreate[Customer Clicks<br/>'Create Request']
    ClickCreate --> CheckDuplicate[GET /api/requests/active]

    CheckDuplicate --> HasActive{Customer Has<br/>Active Request?}
    HasActive -->|Yes| RejectDupe[❌ Error: Already Have<br/>Active Request]
    RejectDupe --> Dashboard

    HasActive -->|No| ShowForm[Show Request Form:<br/>Special Instructions<br/>Preferred Schedule]
    ShowForm --> FillForm[Customer Fills Form]
    FillForm --> SubmitRequest[POST /api/requests/create]

    %% Server Validates and Creates Request
    SubmitRequest --> ValidateRequest{Request<br/>Valid?}
    ValidateRequest -->|No| ValidationError[❌ Validation Error<br/>Missing Fields]
    ValidationError --> ShowForm

    ValidateRequest -->|Yes| CreateRequestDB[(INSERT INTO Requests<br/>Status = Pending<br/>CustomerId<br/>CreatedAt = Now<br/>TotalCost = 0)]

    CreateRequestDB --> LogRequestCreated[(SystemLogs Table<br/>Request Created<br/>CustomerId, RequestId)]

    %% Auto-Assignment Algorithm
    LogRequestCreated --> TriggerQueue[⚙️ Background Queue Processor<br/>Triggered]
    TriggerQueue --> GetRobots[Query: Get All<br/>Online Active Robots]

    GetRobots --> AnyRobots{Any Robots<br/>Online?}
    AnyRobots -->|No| NoRobotsAvail[❌ No Robots Available<br/>Stay in Pending Queue]
    NoRobotsAvail --> NotifyPending[📧 Email: Request Pending<br/>Admin Approval Needed]
    NotifyPending --> WaitInQueue[Wait for Admin<br/>or Robot to Become Available]

    AnyRobots -->|Yes| FindAvailable[Find Robots with<br/>Status = Available]
    FindAvailable --> HasAvailable{Found Available<br/>Robot?}

    HasAvailable -->|No| FindBusy[Find Busy Robots<br/>Check Current Tasks]
    FindBusy --> SelectLRU[Select Least Recently<br/>Used Busy Robot]
    SelectLRU --> ReassignRobot[Reset Current Request<br/>to Pending]
    ReassignRobot --> AssignToNew[Assign Robot to<br/>New Request]

    HasAvailable -->|Yes| SelectFirst[Select First<br/>Available Robot]
    SelectFirst --> AssignToNew

    AssignToNew --> UpdateAssignment[(UPDATE Requests<br/>SET AssignedRobotName<br/>UPDATE Robots<br/>SET Status = Busy)]

    UpdateAssignment --> CheckAutoAccept{Auto-Accept<br/>Setting Enabled?}

    CheckAutoAccept -->|No| SetPending[(UPDATE Requests<br/>SET Status = Pending)]
    SetPending --> WaitAdminApproval[⏳ Wait for Admin Approval]

    WaitAdminApproval --> AdminDashboard[👨‍💼 Admin Opens<br/>Request Dashboard]
    AdminDashboard --> AdminSees[Admin Sees Pending Request<br/>Customer Info<br/>Assigned Robot<br/>Request Details]

    AdminSees --> AdminDecision{Admin<br/>Action?}
    AdminDecision -->|Decline| EnterDeclineReason[Admin Enters<br/>Decline Reason]
    EnterDeclineReason --> DeclineRequest[(UPDATE Requests<br/>SET Status = Declined<br/>DeclineReason<br/>DeclinedAt = Now)]
    DeclineRequest --> NotifyDeclined[📧 Email Customer:<br/>Request Declined]
    NotifyDeclined --> LogDeclined[(SystemLogs Table<br/>Request Declined<br/>AdminId, Reason)]
    LogDeclined --> EndDeclined([END: Request Declined])

    AdminDecision -->|Accept| AcceptRequest[Admin Clicks Accept]
    AcceptRequest --> CheckAutoAccept

    CheckAutoAccept -->|Yes| CheckOtherBusy{Any Other<br/>Active Requests?}
    CheckOtherBusy -->|Yes| SetPending
    CheckOtherBusy -->|No| SetAccepted

    AcceptRequest --> SetAccepted[(UPDATE Requests<br/>SET Status = Accepted<br/>AcceptedAt = Now)]

    %% Navigation Target Setup
    SetAccepted --> LogAccepted[(SystemLogs Table<br/>Request Accepted<br/>RobotId, RequestId)]
    LogAccepted --> GetCustomerBeacon[Query: Get Customer's<br/>Assigned Beacon]

    GetCustomerBeacon --> HasBeacon{Customer Has<br/>Beacon?}
    HasBeacon -->|No| ErrorNoBeacon[❌ Error: No Beacon Assigned<br/>Admin Must Configure]
    ErrorNoBeacon --> CancelRequest

    HasBeacon -->|Yes| GetBeaconDetails[(Query Beacons Table<br/>Get MAC, Room, Threshold)]
    GetBeaconDetails --> SetNavTarget[Set Beacon as<br/>NavigationTarget = true]

    %% Robot Data Exchange (Continuous Loop)
    SetNavTarget --> RobotPolling[🤖 Robot Data Exchange<br/>POST /api/robot/NAME/data-exchange<br/>Every 1 Second]

    RobotPolling --> RobotSendsData[Robot Sends:<br/>- Detected Beacons array<br/>- Weight in kg<br/>- UltrasonicDistance<br/>- IsInTarget boolean<br/>- Timestamp]

    RobotSendsData --> ServerProcesses[Server Processes Data]
    ServerProcesses --> CheckArrivalFlag{IsInTarget<br/>= true?}

    CheckArrivalFlag -->|No| ServerResponds[Server Responds:<br/>- ActiveBeacons config<br/>- IsLineFollowing = true<br/>- FollowColor<br/>- NavigationTargets]

    ServerResponds --> RobotReceives[Robot Receives Config]
    RobotReceives --> RobotExecutes[🤖 Robot Actions:<br/>1. Start Line Following<br/>2. Apply PID Control<br/>3. Scan for Beacons<br/>4. Check RSSI Thresholds]

    RobotExecutes --> CameraReads[📷 Camera: Capture Frame<br/>Detect Line Position]
    CameraReads --> PIDCalc[PID Controller:<br/>Calculate Motor Output<br/>Error Correction]
    PIDCalc --> MotorControl[🎛️ Motor Driver:<br/>Adjust Speed/Direction]

    MotorControl --> BeaconScan[📡 Bluetooth: Scan BLE<br/>Detect Beacons<br/>Measure RSSI]
    BeaconScan --> CheckBeaconRSSI{Target Beacon<br/>RSSI >= Threshold?}

    CheckBeaconRSSI -->|No| ContinueNav[Continue Navigation<br/>Follow Line]
    ContinueNav --> WaitNextPoll[⏱️ Wait 1 Second]
    WaitNextPoll --> RobotPolling

    %% Robot Arrives at Customer Room
    CheckBeaconRSSI -->|Yes| SetIsInTarget[Robot Sets:<br/>IsInTarget = true]
    SetIsInTarget --> RobotPolling

    CheckArrivalFlag -->|Yes| GetRequestStatus[Get Current<br/>Request Status]
    GetRequestStatus --> CheckStatus{Request<br/>Status?}

    CheckStatus -->|Accepted| ArrivedAtRoom[(UPDATE Requests<br/>SET Status = ArrivedAtRoom<br/>ArrivedAt = Now)]
    ArrivedAtRoom --> StopLineFollow[Server Responds:<br/>IsLineFollowing = false]
    StopLineFollow --> RobotStops[🤖 Robot Stops<br/>🔊 Buzzer: Alert Customer]

    RobotStops --> NotifyArrived[📧 Push Notification:<br/>'Robot has arrived!<br/>Please load laundry']
    NotifyArrived --> LogArrival[(SystemLogs Table<br/>Robot Arrived<br/>RequestId, Location)]

    LogArrival --> CustomerOpensApp[👤 Customer Opens App<br/>Sees 'Robot Waiting']
    CustomerOpensApp --> WeightMonitor[⚖️ Weight Sensor<br/>Continuous Reading<br/>HX711 Load Cell]

    WeightMonitor --> CustomerLoads[Customer Loads Laundry<br/>into Robot Basket]
    CustomerLoads --> WeightIncreases[Weight Increases:<br/>Reading > Min Threshold]

    WeightIncreases --> CheckWeight{Weight ><br/>0.5 kg?}
    CheckWeight -->|No| WaitLoad[⏳ Wait for Loading<br/>Timeout: 10 minutes]
    WaitLoad --> CheckTimeout1{Timeout<br/>Exceeded?}
    CheckTimeout1 -->|Yes| TimeoutCancel[⏰ Timeout!<br/>Auto-Cancel Request]
    TimeoutCancel --> CancelRequest
    CheckTimeout1 -->|No| WeightMonitor

    CheckWeight -->|Yes| CheckMaxWeight{Weight <<br/>Max (50kg)?}
    CheckMaxWeight -->|No| OverweightAlert[❌ Overweight!<br/>Alert Customer]
    OverweightAlert --> WeightMonitor

    CheckMaxWeight -->|Yes| EnableConfirm[✅ Enable<br/>'Confirm Loaded' Button]
    EnableConfirm --> CustomerConfirms[Customer Clicks<br/>'Confirm Loaded']

    CustomerConfirms --> RecordWeight[POST /api/requests/ID/confirm-loaded]
    RecordWeight --> CalcCost[Calculate Cost:<br/>TotalCost = Weight × RatePerKg<br/>Default: ₱50/kg]

    CalcCost --> UpdateLoaded[(UPDATE Requests<br/>SET Status = LaundryLoaded<br/>Weight = weight<br/>TotalCost = cost<br/>LoadedAt = Now)]

    UpdateLoaded --> LogLoaded[(SystemLogs Table<br/>Laundry Loaded<br/>Weight, Cost)]

    %% Return to Base
    LogLoaded --> SetBaseTarget[Set Base Beacon as<br/>NavigationTarget]
    SetBaseTarget --> RobotPolling2[🤖 Robot Data Exchange<br/>Continues Every 1s]

    RobotPolling2 --> ServerSendsBase[Server Responds:<br/>IsLineFollowing = true<br/>NavigationTarget = Base Beacon]
    ServerSendsBase --> RobotReturns[🤖 Robot Navigates<br/>Back to Base<br/>Line Following Active]

    RobotReturns --> BeaconScanBase[📡 Scan for Base Beacon]
    BeaconScanBase --> CheckBaseRSSI{Base Beacon<br/>RSSI >= Threshold?}

    CheckBaseRSSI -->|No| ContinueReturn[Continue to Base<br/>Follow Line]
    ContinueReturn --> WaitPoll2[⏱️ Wait 1 Second]
    WaitPoll2 --> RobotPolling2

    CheckBaseRSSI -->|Yes| RobotAtBase[Robot Sets:<br/>IsInTarget = true]
    RobotAtBase --> RobotPolling2

    RobotPolling2 --> CheckStatusBase{Request Status?}
    CheckStatusBase -->|LaundryLoaded| UpdateWashing[(UPDATE Requests<br/>SET Status = Washing<br/>UPDATE Robots<br/>SET Status = Available)]

    UpdateWashing --> RobotAvailable[🤖 Robot Now Available<br/>Can Accept New Requests]
    RobotAvailable --> ProcessNextQueue[⚙️ Queue Processor:<br/>Check for Pending Requests]

    ProcessNextQueue --> NotifyWashing[📧 Notification:<br/>'Laundry is being washed']
    NotifyWashing --> LogWashing[(SystemLogs Table<br/>Washing Started<br/>RobotId, RequestId)]

    %% Admin Washing Process
    LogWashing --> AdminWashes[👨‍💼 Admin Physically<br/>Washes Laundry]
    AdminWashes --> AdminOpensDash[Admin Opens Dashboard<br/>Sees 'Washing' Requests]

    AdminOpensDash --> AdminFinishes[Admin Finishes Washing<br/>Clicks 'Mark Done']
    AdminFinishes --> MarkDone[POST /api/requests/ID/mark-washing-done]

    MarkDone --> UpdateFinished[(UPDATE Requests<br/>SET Status = FinishedWashing<br/>FinishedWashingAt = Now)]
    UpdateFinished --> LogFinished[(SystemLogs Table<br/>Washing Completed)]

    LogFinished --> AdminStartsDelivery[Admin Clicks<br/>'Start Delivery']
    AdminStartsDelivery --> CheckRobotsOnline{Any Robots<br/>Online?}

    CheckRobotsOnline -->|No| ErrorNoBots[❌ Error: No Robots Available<br/>Cannot Start Delivery]
    ErrorNoBots --> WaitForRobot[⏳ Wait for Robot<br/>to Come Online]

    CheckRobotsOnline -->|Yes| FindDeliveryRobot[Find Available Robot<br/>or Assign Least Busy]
    FindDeliveryRobot --> AssignDelivery[(UPDATE Requests<br/>SET Status = FinishedWashingGoingToRoom<br/>AssignedRobotName)]

    AssignDelivery --> LogDeliveryStart[(SystemLogs Table<br/>Delivery Started<br/>RobotId)]

    %% Delivery Navigation
    LogDeliveryStart --> SetCustomerBeacon[Set Customer Beacon<br/>as NavigationTarget]
    SetCustomerBeacon --> RobotPolling3[🤖 Robot Data Exchange<br/>Every 1 Second]

    RobotPolling3 --> ServerSendsCustomer[Server Responds:<br/>IsLineFollowing = true<br/>NavigationTarget = Customer Beacon]
    ServerSendsCustomer --> RobotDelivers[🤖 Robot Navigates<br/>to Customer Room<br/>Carrying Clean Laundry]

    RobotDelivers --> BeaconScanCustomer[📡 Scan for Customer Beacon]
    BeaconScanCustomer --> CheckCustomerRSSI{Customer Beacon<br/>RSSI >= Threshold?}

    CheckCustomerRSSI -->|No| ContinueDelivery[Continue Navigation<br/>Follow Line]
    ContinueDelivery --> WaitPoll3[⏱️ Wait 1 Second]
    WaitPoll3 --> RobotPolling3

    CheckCustomerRSSI -->|Yes| RobotArrivedDelivery[Robot Sets:<br/>IsInTarget = true]
    RobotArrivedDelivery --> RobotPolling3

    RobotPolling3 --> CheckStatusDelivery{Request Status?}
    CheckStatusDelivery -->|FinishedWashingGoingToRoom| ArrivedForDelivery[(UPDATE Requests<br/>SET Status = FinishedWashingArrivedAtRoom<br/>ArrivedForDeliveryAt = Now)]

    ArrivedForDelivery --> RobotStopsDelivery[🤖 Robot Stops<br/>🔊 Buzzer: Alert]
    RobotStopsDelivery --> NotifyDeliveryArrived[📧 Push Notification:<br/>'Clean laundry arrived!<br/>Please unload']

    NotifyDeliveryArrived --> LogDeliveryArrival[(SystemLogs Table<br/>Delivery Arrival)]

    %% Customer Unloads
    LogDeliveryArrival --> CustomerOpensApp2[👤 Customer Opens App<br/>Sees 'Unload Laundry']
    CustomerOpensApp2 --> WeightMonitorUnload[⚖️ Weight Sensor<br/>Monitoring]

    WeightMonitorUnload --> CustomerUnloads[Customer Unloads<br/>Clean Laundry]
    CustomerUnloads --> WeightDecreases[Weight Decreases:<br/>Reading < Min Threshold]

    WeightDecreases --> CheckUnloadWeight{Weight <<br/>0.5 kg?}
    CheckUnloadWeight -->|No| WaitUnload[⏳ Wait for Unloading<br/>Timeout: 10 minutes]
    WaitUnload --> CheckTimeout2{Timeout<br/>Exceeded?}
    CheckTimeout2 -->|Yes| TimeoutCancelUnload[⏰ Timeout!<br/>Auto-Cancel]
    TimeoutCancelUnload --> CancelRequest
    CheckTimeout2 -->|No| WeightMonitorUnload

    CheckUnloadWeight -->|Yes| EnableUnloadConfirm[✅ Enable<br/>'Confirm Unloaded' Button]
    EnableUnloadConfirm --> CustomerConfirmsUnload[Customer Clicks<br/>'Confirm Unloaded']

    CustomerConfirmsUnload --> RecordUnload[POST /api/requests/ID/confirm-unloaded]
    RecordUnload --> UpdateUnloaded[(UPDATE Requests<br/>SET Status = FinishedWashingGoingToBase<br/>UnloadedAt = Now)]

    UpdateUnloaded --> LogUnloaded[(SystemLogs Table<br/>Laundry Unloaded)]

    %% Final Return to Base
    LogUnloaded --> SetBaseFinal[Set Base Beacon as<br/>NavigationTarget]
    SetBaseFinal --> RobotPolling4[🤖 Robot Data Exchange<br/>Every 1 Second]

    RobotPolling4 --> RobotReturnsFinal[🤖 Robot Returns<br/>to Base<br/>Empty Basket]
    RobotReturnsFinal --> CheckBaseFinal{Base Beacon<br/>RSSI >= Threshold?}

    CheckBaseFinal -->|No| ContinueFinalReturn[Continue to Base]
    ContinueFinalReturn --> WaitPoll4[⏱️ Wait 1 Second]
    WaitPoll4 --> RobotPolling4

    CheckBaseFinal -->|Yes| RobotAtBaseFinal[Robot Sets:<br/>IsInTarget = true]
    RobotAtBaseFinal --> RobotPolling4

    RobotPolling4 --> UpdateCompleted[(UPDATE Requests<br/>SET Status = Completed<br/>CompletedAt = Now<br/>UPDATE Robots<br/>SET Status = Available)]

    UpdateCompleted --> RobotAvailableFinal[🤖 Robot Available<br/>for Next Request]
    RobotAvailableFinal --> ProcessQueueFinal[⚙️ Queue Processor:<br/>Process Next Pending]

    ProcessQueueFinal --> NotifyCompleted[📧 Notification:<br/>'Service Completed!<br/>Please proceed to payment']
    NotifyCompleted --> LogCompleted[(SystemLogs Table<br/>Request Completed<br/>Duration, Cost)]

    %% Payment Processing
    LogCompleted --> CreatePayment[(INSERT INTO Payments<br/>LaundryRequestId<br/>Amount = TotalCost<br/>Status = Pending<br/>CreatedAt = Now)]

    CreatePayment --> CustomerSeesPayment[👤 Customer Opens App<br/>Sees Payment Due]
    CustomerSeesPayment --> AdminPaymentDash[👨‍💼 Admin Opens<br/>Accounting Dashboard]

    AdminPaymentDash --> ShowMetrics[📊 Display Metrics:<br/>- Total Revenue<br/>- Outstanding Payments<br/>- Completed Payments<br/>- Pending Requests]

    ShowMetrics --> AdminSelectsPayment[Admin Selects<br/>Pending Payment]
    AdminSelectsPayment --> AdminPaymentAction{Admin<br/>Action?}

    AdminPaymentAction -->|Mark as Paid| SelectMethod[Select Payment Method:<br/>- Cash<br/>- GCash]
    SelectMethod --> RecordPayment[POST /api/payment/ID/mark-paid]

    RecordPayment --> UpdatePaymentPaid[(UPDATE Payments<br/>SET Status = Completed<br/>Method = Cash or GCash<br/>CompletedAt = Now)]

    UpdatePaymentPaid --> CreateAdjustment[(INSERT INTO PaymentAdjustments<br/>Type = CompletePayment<br/>Amount = amount<br/>Description<br/>CreatedAt = Now)]

    CreateAdjustment --> LogPayment[(SystemLogs Table<br/>Payment Completed<br/>Method, Amount)]

    LogPayment --> CalcRevenue[💰 Calculate Total Revenue:<br/>SUM Completed Payments<br/>- SUM Refunds<br/>+ SUM AddRevenue Adjustments<br/>- SUM Expenses]

    CalcRevenue --> UpdateDashboard[Update Accounting Dashboard<br/>Real-time Metrics]

    UpdateDashboard --> GenerateReceipt[📄 Generate Receipt:<br/>Number: RCP-YYYY-NNNNNN<br/>Customer: Name<br/>Weight: kg<br/>Rate: ₱50/kg<br/>Total: ₱amount<br/>Method: method<br/>Date: date]

    GenerateReceipt --> CustomerViewReceipt[👤 Customer Opens<br/>'View Receipt']
    CustomerViewReceipt --> DisplayReceipt[📱 Display Receipt:<br/>Printable Format<br/>Share Option]

    %% Admin Reporting
    DisplayReceipt --> AdminReporting{Admin Generate<br/>Report?}
    AdminReporting -->|Yes| SelectPeriod[Select Period:<br/>- Today<br/>- This Week<br/>- This Month<br/>- Custom Range]

    SelectPeriod --> QueryReports[(Query Database:<br/>Filter Payments by Date<br/>JOIN Requests<br/>GROUP BY Customer and Method)]

    QueryReports --> GenerateReport[📊 Generate Sales Report:<br/>- Revenue by Method<br/>- Top Customers<br/>- Transaction Count<br/>- Average Transaction<br/>- Daily/Weekly Trends]

    GenerateReport --> ExportChoice{Export<br/>Format?}
    ExportChoice -->|CSV| ExportCSV[💾 Download CSV File<br/>Excel Compatible]
    ExportChoice -->|PDF| ExportPDF[💾 Download PDF Report<br/>Print Ready]
    ExportChoice -->|View| DisplayHTML[🖥️ Display in Browser<br/>Interactive Charts]

    ExportCSV --> EndPayment
    ExportPDF --> EndPayment
    DisplayHTML --> EndPayment
    AdminReporting -->|No| EndPayment

    EndPayment([✅ END: Request Complete<br/>Payment Recorded<br/>Customer Satisfied])

    %% Messaging System (Parallel Process)
    Dashboard -.->|Customer Needs Help| OpenMessages[📧 Open Messages]
    OpenMessages --> LoadConversation[GET /api/messages/conversation]
    LoadConversation --> DisplayMessages[Display Message Thread<br/>Previous Messages]

    DisplayMessages --> CustomerSendsMsg[Customer Types Message<br/>Optional: Attach Image]
    CustomerSendsMsg --> SendMessage[POST /api/messages/send]

    SendMessage --> SaveMessage[(INSERT INTO Messages<br/>FromCustomerId<br/>Content<br/>ImagePath if attached<br/>IsReadByAdmin = false<br/>CreatedAt = Now)]

    SaveMessage --> NotifyAdmin[📧 Email Admin:<br/>New Customer Message]
    NotifyAdmin --> AdminOpensMessages[👨‍💼 Admin Opens<br/>Message Center]

    AdminOpensMessages --> ShowConversations[Show All Conversations<br/>Unread Count Badge]
    ShowConversations --> AdminSelectsConvo[Admin Selects<br/>Customer Conversation]

    AdminSelectsConvo --> MarkAsRead[(UPDATE Messages<br/>SET IsReadByAdmin = true<br/>WHERE CustomerId = id)]

    MarkAsRead --> AdminResponds[Admin Types Response<br/>Optional: Attach Image]
    AdminResponds --> SendAdminMsg[POST /api/messages/send]

    SendAdminMsg --> SaveAdminMsg[(INSERT INTO Messages<br/>FromAdminId<br/>ToCustomerId<br/>Content<br/>ImagePath<br/>CreatedAt = Now)]

    SaveAdminMsg --> NotifyCustomerMsg[📧 Push Notification:<br/>Admin Replied]
    NotifyCustomerMsg --> LogMessage[(SystemLogs Table<br/>Message Exchange)]

    LogMessage -.-> Dashboard

    %% Cancellation Flow (Parallel Process)
    Dashboard -.->|Customer Cancels| CancelRequest[Customer Clicks<br/>'Cancel Request']
    WaitAdminApproval -.->|Admin Cancels| CancelRequest

    CancelRequest --> ConfirmCancel{Confirm<br/>Cancellation?}
    ConfirmCancel -->|No| Dashboard
    ConfirmCancel -->|Yes| GetCancelStatus[Get Current<br/>Request Status]

    GetCancelStatus --> CancelStatusCheck{Status?}

    CancelStatusCheck -->|Pending| SimpleCancelDB[(UPDATE Requests<br/>SET Status = Cancelled<br/>CancelledAt = Now)]
    SimpleCancelDB --> LogCancel

    CancelStatusCheck -->|Accepted/ArrivedAtRoom/LaundryLoaded| SendRobotBack[(UPDATE Requests<br/>SET Status = Cancelled<br/>SET NavigationTarget = Base)]
    SendRobotBack --> RobotReturnsCancel[🤖 Robot Returns to Base<br/>Cancel Navigation]
    RobotReturnsCancel --> LogCancel

    CancelStatusCheck -->|Washing/FinishedWashing| AdminIntervention[❌ Cannot Auto-Cancel<br/>Require Admin Action]
    AdminIntervention --> AdminHandlesCancel[Admin Manually<br/>Resolves Cancellation]
    AdminHandlesCancel --> LogCancel

    LogCancel[(SystemLogs Table<br/>Request Cancelled<br/>Reason, Timestamp)]
    LogCancel --> ProcessRefund[💰 Process Refund<br/>if Payment Made]
    ProcessRefund --> NotifyCancelled[📧 Notification:<br/>Request Cancelled]
    NotifyCancelled --> EndCancelled([END: Request Cancelled])

    %% Offline Robot Handling (Background Service)
    RobotPolling -.->|No Response| OfflineDetector[⚙️ Offline Detector Service<br/>Checks Every 30s]
    OfflineDetector --> CheckLastPing{LastPing ><br/>90 Seconds?}

    CheckLastPing -->|Yes| MarkOffline[(UPDATE Robots<br/>SET IsOffline = true<br/>Status = Offline)]

    MarkOffline --> GetAssignedReq[Get Robot's<br/>Active Request]
    GetAssignedReq --> HasAssigned{Has Active<br/>Request?}

    HasAssigned -->|Yes| ReassignOrCancel[Reassign to Another Robot<br/>OR Cancel Request]
    HasAssigned -->|No| LogOffline

    ReassignOrCancel --> NotifyOffline[📧 Alert Admin:<br/>Robot Offline]
    NotifyOffline --> LogOffline[(SystemLogs Table<br/>Robot Offline<br/>Critical Alert)]

    LogOffline --> WaitReconnect[⏳ Monitor for Reconnection]
    CheckLastPing -->|No| RobotHealthy[Robot Healthy<br/>Continue Monitoring]

    %% Styling
    classDef customer fill:#A8D5BA,stroke:#5A9279,stroke-width:3px,color:#2C4A3A
    classDef admin fill:#F4D19B,stroke:#D4A574,stroke-width:3px,color:#6B4E2A
    classDef robot fill:#6B9AC4,stroke:#3D5A80,stroke-width:3px,color:#1E3A5F
    classDef database fill:#B8A4C9,stroke:#9181A8,stroke-width:2px,color:#4A3E5A
    classDef api fill:#A4C5A8,stroke:#7A9E7F,stroke-width:2px,color:#3A5A3F
    classDef error fill:#E8A0A0,stroke:#C67373,stroke-width:2px,color:#6B3A3A
    classDef success fill:#A8D5BA,stroke:#5A9279,stroke-width:2px,color:#2C4A3A
    classDef process fill:#F5B895,stroke:#D49470,stroke-width:2px,color:#6B4830
    classDef log fill:#D4C5E8,stroke:#A89BC4,stroke-width:2px,color:#5A4E7A

    class Start,Dashboard,CustomerOpensApp,CustomerOpensApp2,CustomerSeesPayment,CustomerViewReceipt customer
    class AdminDashboard,AdminOpensDash,AdminPaymentDash,AdminOpensMessages admin
    class RobotPolling,RobotPolling2,RobotPolling3,RobotPolling4,RobotExecutes,RobotStops robot
    class CreateRequestDB,UpdateAssignment,UpdateLoaded,UpdateWashing,UpdateCompleted,CreatePayment,UpdatePaymentPaid,SaveMessage database
    class LoginAPI,SubmitRequest,RecordWeight,RecordPayment,SendMessage api
    class LoginFail,RejectDupe,ValidationError,ErrorNoBeacon,OverweightAlert,TimeoutCancel,ErrorNoBots error
    class EndPayment,EndCancelled,EndDeclined success
    class TriggerQueue,ProcessNextQueue,OfflineDetector process
    class LogFailedLogin,LogSuccessLogin,LogRequestCreated,LogAccepted,LogArrival,LogCompleted,LogPayment,LogCancel,LogOffline log
```

---

## Complete System Flow Description

### Phase 1: Authentication (Lines 1-30)
**Customer authenticates** → Validates credentials → Generates JWT token → Stores in SecureStorage → Logs successful login → Enters dashboard

**Key Components:**
- Mobile App authentication screen
- `/api/auth/login` endpoint
- Users table validation
- JWT token generation
- SystemLogs table (login attempts)

---

### Phase 2: Request Creation (Lines 31-60)
**Customer creates request** → Validates no duplicate → Submits form → Creates request in database → Triggers queue processor

**Key Components:**
- Request form validation
- `/api/requests/create` endpoint
- Requests table INSERT
- SystemLogs (request created)
- Background queue processor triggered

---

### Phase 3: Robot Assignment (Lines 61-100)
**Queue processor finds available robot** → Assigns robot → Checks auto-accept setting → Either accepts immediately or waits for admin approval

**Key Components:**
- Robot availability query
- Auto-assignment algorithm
- Admin approval workflow (if needed)
- Request status updates
- Robot status updates (Available → Busy)

---

### Phase 4: Navigation to Customer (Lines 101-150)
**Robot navigates to customer** → Line following with PID control → Beacon scanning → RSSI threshold detection → Arrival confirmation

**Key Components:**
- Beacon navigation target setup
- Robot data exchange (1-second polling)
- Line following algorithm
- BLE beacon scanning
- Arrival detection (RSSI-based)
- Weight sensor monitoring

---

### Phase 5: Laundry Loading (Lines 151-180)
**Robot arrives** → Customer loads laundry → Weight sensor detects load → Customer confirms → Cost calculated

**Key Components:**
- Weight sensor (HX711 load cell)
- Load confirmation endpoint
- Cost calculation (Weight × RatePerKg)
- Timeout monitoring
- Overweight validation

---

### Phase 6: Return to Base (Lines 181-210)
**Robot returns to base** → Navigation with base beacon target → Arrives at base → Status updated to "Washing"

**Key Components:**
- Base beacon navigation
- Arrival detection at base
- Robot status → Available
- Request status → Washing
- Queue processor activates (robot now free)

---

### Phase 7: Washing Process (Lines 211-240)
**Admin washes laundry** → Marks washing done → Starts delivery → Robot assigned for delivery

**Key Components:**
- Admin dashboard (washing management)
- Manual washing confirmation
- Delivery initiation
- Robot reassignment for delivery

---

### Phase 8: Delivery to Customer (Lines 241-280)
**Robot navigates back to customer** → Carries clean laundry → Arrives → Customer unloads

**Key Components:**
- Navigation to customer beacon (again)
- Delivery arrival notification
- Weight sensor (unload detection)
- Unload confirmation

---

### Phase 9: Final Return to Base (Lines 281-310)
**Robot returns to base** → Completes request → Robot becomes available → Request marked completed

**Key Components:**
- Final navigation to base
- Request completion
- Robot availability update
- Queue processor (next request)

---

### Phase 10: Payment & Accounting (Lines 311-370)
**Payment created** → Admin marks as paid → Payment recorded → Revenue calculated → Receipt generated → Reports generated

**Key Components:**
- Payments table
- PaymentAdjustments table
- Revenue calculation
- Receipt generation
- Sales reports (CSV/PDF export)
- Accounting dashboard metrics

---

### Phase 11: Messaging System (Lines 371-410, Parallel)
**Customer sends message** → Admin receives notification → Admin responds → Customer notified

**Key Components:**
- Messages table
- Email notifications
- Image attachments
- Read receipts
- Real-time polling

---

### Phase 12: Cancellation Handling (Lines 411-450, Parallel)
**Request cancelled** → Status checked → Robot returned (if needed) → Refund processed → Logged

**Key Components:**
- Cancellation validation
- Robot return-to-base
- Admin intervention (if washing)
- Refund processing
- SystemLogs

---

### Phase 13: Offline Robot Detection (Lines 451-480, Background)
**Robot disconnects** → Offline detector triggers → Robot marked offline → Request reassigned → Admin alerted

**Key Components:**
- Background service (30s interval)
- LastPing monitoring
- Robot health checks
- Request reassignment
- Critical alerts

---

## Database Tables Used

| Table | Operations | Data Stored |
|-------|-----------|-------------|
| **Users** | SELECT (auth), UPDATE (profile) | Credentials, roles, profile, beacon assignment |
| **Requests** | INSERT, UPDATE (status changes) | Status, timestamps, costs, robot assignment |
| **Robots** | SELECT (assignment), UPDATE (status) | Status, last ping, IP address, current task |
| **Beacons** | SELECT (navigation) | MAC address, RSSI threshold, room mapping |
| **Messages** | INSERT, SELECT, UPDATE (read status) | Chat content, images, timestamps, read flags |
| **Payments** | INSERT, UPDATE (status) | Amount, method, status, timestamps |
| **PaymentAdjustments** | INSERT | Revenue adjustments, expenses, types |
| **SystemLogs** | INSERT (all events) | Event type, entity IDs, timestamps, descriptions |

---

## API Endpoints Called

| Endpoint | Method | Purpose | Caller |
|----------|--------|---------|--------|
| `/api/auth/login` | POST | Authentication | Mobile App |
| `/api/requests/create` | POST | Create request | Mobile App |
| `/api/requests/active` | GET | Check duplicates | Mobile App |
| `/api/requests/{id}/confirm-loaded` | POST | Confirm laundry loaded | Mobile App |
| `/api/requests/{id}/confirm-unloaded` | POST | Confirm unloaded | Mobile App |
| `/api/requests/{id}/mark-washing-done` | POST | Mark washing complete | Web Admin |
| `/api/robot/{name}/data-exchange` | POST | Bidirectional data | Robot (1s interval) |
| `/api/payment/{id}/mark-paid` | POST | Record payment | Web Admin |
| `/api/messages/send` | POST | Send message | Mobile/Web |
| `/api/messages/conversation` | GET | Get chat history | Mobile/Web |

---

## Background Services

| Service | Interval | Trigger | Action |
|---------|----------|---------|--------|
| **Queue Processor** | Continuous | New request or robot available | Assigns robots to pending requests |
| **Timeout Monitor** | 10 seconds | Active requests | Cancels requests exceeding time limits |
| **Offline Detector** | 30 seconds | All robots | Marks offline robots, reassigns requests |
| **Log Cleaner** | Daily | Scheduled | Archives old logs to file system |

---

## Robot Hardware Components

| Component | Model/Type | Data Reported |
|-----------|-----------|---------------|
| **Camera** | Raspberry Pi Camera | Line position (center offset) |
| **Weight Sensor** | HX711 Load Cell | Weight in kg |
| **Ultrasonic** | HC-SR04 | Distance in cm |
| **Bluetooth** | Built-in BLE | Beacon RSSI values |
| **Motor Driver** | L298N | N/A (receives commands) |
| **Buzzer** | Piezo buzzer | N/A (receives commands) |

---

## System Logging Events

All events logged to `SystemLogs` table:

| Event | Entity | Data Logged |
|-------|--------|-------------|
| Login Attempt | User | Success/Failure, IP, Timestamp |
| Request Created | Request | CustomerId, RequestId, Timestamp |
| Request Accepted | Request | AdminId, RobotId, Timestamp |
| Robot Arrived | Request | Location, RSSI, Timestamp |
| Laundry Loaded | Request | Weight, Cost, Timestamp |
| Washing Started | Request | RobotId, Timestamp |
| Delivery Started | Request | RobotId, Timestamp |
| Request Completed | Request | Duration, Cost, Timestamp |
| Payment Completed | Payment | Method, Amount, Timestamp |
| Message Sent | Message | SenderId, ReceiverId, Timestamp |
| Request Cancelled | Request | Reason, InitiatorId, Timestamp |
| Robot Offline | Robot | LastPing, Timestamp |

---

## Color Legend

- 🟢 **Soft Green** - Customer-facing actions and success states
- 🟡 **Soft Amber** - Administrator actions
- 🔵 **Muted Blue** - Robot operations
- 🟣 **Soft Lavender** - Database operations
- 🌿 **Muted Mint** - API endpoints
- 🔴 **Soft Coral** - Errors and failures
- 🟠 **Soft Peach** - Background processes
- 💜 **Pale Purple** - System logging

---

## Key Takeaways

This comprehensive DFD demonstrates:

✅ **Complete end-to-end flow** from login to payment
✅ **All features integrated**: Authentication, robot control, payment, messaging, logging
✅ **Real-time communication**: 1-second robot polling, push notifications
✅ **Error handling**: Timeouts, cancellations, offline detection
✅ **Financial tracking**: Payments, adjustments, revenue calculation, reporting
✅ **Audit trail**: Complete system logging for all events
✅ **Multi-actor system**: Customers, admins, robots all interconnected
✅ **Background automation**: Queue processing, timeout monitoring, health checks

**Document Version:** 1.0
**Created:** 2025
**For:** Thesis Defense - Complete System Documentation
