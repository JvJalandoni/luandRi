import { Platform } from 'react-native';

/**
 * Notification Service - NO-OP implementation
 * expo-notifications was removed due to incompatibility with React Native 0.79.5
 * All methods are stubs that do nothing
 */
class NotificationService {
  private isInitialized = false;

  /**
   * Initialize notification service (NO-OP)
   */
  async initialize() {
    if (this.isInitialized) return;
    this.isInitialized = true;
    console.log('⚠️ Notification service disabled (expo-notifications removed)');
  }

  /**
   * Request notification permissions (NO-OP - always returns false)
   */
  async requestPermissions(): Promise<boolean> {
    console.log('⚠️ Notifications disabled - returning false');
    return false;
  }

  /**
   * Check if permissions are granted (NO-OP - always returns false)
   */
  async hasPermissions(): Promise<boolean> {
    return false;
  }

  /**
   * Send a test notification (NO-OP)
   */
  async sendTestNotification(): Promise<void> {
    console.log('⚠️ Notifications disabled - test notification not sent');
  }

  /**
   * Send status change notification (NO-OP)
   */
  async sendStatusNotification(
    status: string,
    requestId: number,
    additionalData?: { weight?: number; totalCost?: number }
  ): Promise<void> {
    console.log(`⚠️ Notifications disabled - would have sent notification for status: ${status} (request #${requestId})`);
  }
}

// Export singleton instance
export const notificationService = new NotificationService();
