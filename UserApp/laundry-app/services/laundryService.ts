import { apiGet, apiPost, apiPut } from './api';

export enum LaundryRequestType {
  Pickup = 0,
  Delivery = 1,
  PickupAndDelivery = 2
}

export enum LaundryRequestStatus {
  Pending = 'Pending',
  Accepted = 'Accepted',
  InProgress = 'InProgress', 
  RobotEnRoute = 'RobotEnRoute',
  ArrivedAtRoom = 'ArrivedAtRoom',
  LaundryLoaded = 'LaundryLoaded',
  ReturnedToBase = 'ReturnedToBase',
  WeighingComplete = 'WeighingComplete',
  PaymentPending = 'PaymentPending',
  Completed = 'Completed',
  Declined = 'Declined',
  Cancelled = 'Cancelled',
  Washing = 'Washing',
  FinishedWashing = 'FinishedWashing',
  FinishedWashingGoingToRoom = 'FinishedWashingGoingToRoom',
  FinishedWashingArrivedAtRoom = 'FinishedWashingArrivedAtRoom',
  FinishedWashingGoingToBase = 'FinishedWashingGoingToBase',
  FinishedWashingAwaitingPickup = 'FinishedWashingAwaitingPickup'
}

export interface LaundryRequest {
  // Simplified request - only essential fields
}

export interface LaundryRequestResponse {
  id: number;
  customerId: string;
  customerName: string;
  customerPhone: string;
  address: string;
  instructions?: string;
  type: LaundryRequestType;
  status: LaundryRequestStatus;
  weight?: number;
  totalCost?: number;
  pricePerKg?: number;
  isPaid: boolean;
  requestedAt: string;
  scheduledAt: string;
  acceptedAt?: string;
  completedAt?: string;
  assignedRobot?: string;
  declineReason?: string;
  assignedBeaconMacAddress?: string;
  estimatedArrival?: string;
  actualArrival?: string;
  arrivedAtRoomAt?: string;
}

export interface WeightConfirmation {
  requestId: number;
  weight: number;
  totalCost: number;
  pricePerKg: number;
  minimumCharge?: number;
}

export interface PaymentConfirmation {
  requestId: number;
  paymentMethod: 'Cash' | 'Card' | 'DigitalWallet' | 'BankTransfer';
  paymentReference?: string;
  notes?: string;
}

/**
 * Service for managing laundry requests from the mobile app
 * Provides functions for creating, tracking, and managing pickup/delivery requests
 */
export const laundryService = {
  /**
   * Creates a new laundry pickup request for the authenticated user
   * Auto-assigns an available robot and adds request to queue
   * @returns Promise with request ID, status, and confirmation message
   */
  async createRequest(): Promise<{ id: number; status: string; message: string }> {
    console.log('Creating request...');
    try {
      const response = await apiPost('/requests', {});
      console.log('Request creation successful:', response.data);
      return response.data;
    } catch (error: any) {
      console.error('Request creation failed:', error.response?.status, error.response?.data);
      throw error;
    }
  },

  /**
   * Gets the current status and details of a specific laundry request
   * @param requestId - ID of the request to check
   * @returns Promise with complete request details including status, timestamps, and cost
   */
  async getRequestStatus(requestId: number): Promise<LaundryRequestResponse> {
    const response = await apiGet(`/requests/status/${requestId}`);
    return response.data;
  },

  /**
   * Gets all laundry requests for the authenticated user
   * Returns complete history of past and current requests
   * @returns Promise with array of all user requests (newest first)
   */
  async getUserRequests(): Promise<LaundryRequestResponse[]> {
    const response = await apiGet('/requests/my-requests');
    if (!response) return []; // Handle 401 gracefully
    return response.data;
  },

  /**
   * Gets the user's currently active laundry request (if any)
   * Active means not completed, cancelled, or declined
   * @returns Promise with active request details, or null if no active request
   */
  async getActiveRequest(): Promise<LaundryRequestResponse | null> {
    const response = await apiGet('/requests/active');
    if (!response) return null; // Handle 401 gracefully
    return response.data || null;
  },

  /**
   * Confirms that laundry has been loaded onto the robot
   * Updates request status to LaundryLoaded, robot will return to base
   * @param requestId - ID of the request to confirm
   * @returns Promise with success status and confirmation message
   */
  async confirmLaundryLoaded(requestId: number): Promise<{ success: boolean; message: string }> {
    const response = await apiPost(`/requests/${requestId}/confirm-loaded`, {});
    return response.data;
  },

  /**
   * Confirms laundry loaded with manual weight entry (if robot weight sensor fails)
   * @param requestId - ID of the request to confirm
   * @param weight - Weight of laundry in kilograms
   * @returns Promise with success status and confirmation message
   */
  async confirmLaundryLoadedWithWeight(requestId: number, weight: number): Promise<{ success: boolean; message: string }> {
    const response = await apiPost(`/requests/${requestId}/confirm-loaded`, { weight });
    return response.data;
  },

  /**
   * Confirms that clean laundry has been unloaded from robot (delivery flow)
   * Robot will return to base after confirmation
   * @param requestId - ID of the request to confirm
   * @returns Promise with success status and confirmation message
   */
  async confirmLaundryUnloaded(requestId: number): Promise<{ success: boolean; message: string }> {
    const response = await apiPost(`/requests/${requestId}/confirm-unloaded`, {});
    return response.data;
  },

  /**
   * Selects delivery option after washing is complete
   * Delivery: Robot brings clean laundry to room
   * Pickup: Customer picks up from laundry area
   * @param requestId - ID of the request
   * @param deliveryType - 'Delivery' or 'Pickup'
   * @returns Promise with success status, message, and updated status
   */
  async selectDeliveryOption(requestId: number, deliveryType: 'Delivery' | 'Pickup'): Promise<{ success: boolean; message: string; status: string }> {
    const response = await apiPost(`/requests/${requestId}/select-delivery-option`, { deliveryType });
    return response.data;
  },

  /**
   * Confirms the measured weight and calculated cost
   * Called after robot weighs laundry and calculates price
   * @param requestId - ID of the request
   * @returns Promise with weight, cost, and pricing details
   */
  async confirmWeightAndCost(requestId: number): Promise<WeightConfirmation> {

    const response = await apiPost(`/requests/${requestId}/confirm-weight`, {});
    return response.data;
  },

  /**
   * Confirms payment for the laundry service
   * Marks request as paid and generates receipt
   * @param requestId - ID of the request to pay for
   * @param paymentData - Payment details (method, reference, notes)
   * @returns Promise with success status and confirmation message
   */
  async confirmPayment(requestId: number, paymentData: PaymentConfirmation): Promise<{ success: boolean; message: string }> {

    const response = await apiPost(`/requests/${requestId}/confirm-payment`, paymentData);
    return response.data;
  },

  /**
   * Cancels a laundry request
   * Can only cancel requests that haven't been completed
   * @param requestId - ID of the request to cancel
   * @param reason - Optional reason for cancellation
   * @returns Promise with success status and confirmation message
   */
  async cancelRequest(requestId: number, reason?: string): Promise<{ success: boolean; message: string }> {

    const response = await apiPost(`/requests/${requestId}/cancel`, { reason });
    return response.data;
  },

  /**
   * Gets current laundry service pricing rates
   * @returns Promise with price per kg, minimum charge, and currency
   */
  async getRatesAndPricing(): Promise<{
    pricePerKg: number;
    minimumCharge: number;
    currency: string;
    effectiveFrom: string;
  }> {

    const response = await apiGet('/requests/pricing');
    return response.data;
  },

  /**
   * Gets real-time robot tracking information for a request
   * Shows robot location, status, and estimated arrival
   * @param requestId - ID of the request to track
   * @returns Promise with robot tracking details
   */
  async trackRobot(requestId: number): Promise<{
    robotName: string;
    currentLocation?: string;
    batteryLevel?: number;
    estimatedArrival?: string;
    status: 'idle' | 'enroute' | 'arrived' | 'returning' | 'maintenance';
    lastUpdate: string;
  }> {

    const response = await apiGet(`/requests/${requestId}/robot-status`);
    return response.data;
  },

  /**
   * Gets robot fleet availability status
   * Shows how many robots are available vs busy/offline
   * Helps user know if they can make a request immediately
   * @returns Promise with robot fleet statistics
   */
/**   * Gets queue status for a specific request   * Shows queue position, total queue length, and estimated wait time   * @param requestId - ID of the request to check   * @returns Promise with queue status details   */  async getQueueStatus(requestId: number): Promise<{    isInQueue: boolean;    queuePosition: number;    totalInQueue: number;    estimatedWaitTimeMinutes: number;    status: string;    requestedAt: string;    message: string;  }> {    const response = await apiGet(`/requests/${requestId}/queue-status`);    return response.data;  },

  async getAvailableRobots(): Promise<{
    totalRobots: number;
    availableRobots: number;
    busyRobots: number;
    offlineRobots: number;
    timestamp: string;
  }> {
    const response = await apiGet('/requests/available-robots');
    return response.data;
  }
};