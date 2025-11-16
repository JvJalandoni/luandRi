import { apiGet, apiPut, apiDelete } from './api';
import AsyncStorage from '@react-native-async-storage/async-storage';
import axios from 'axios';

export interface Admin {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string | null;
  isActive: boolean;
}

export interface UserProfile {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  roomName?: string;
  roomDescription?: string;
  assignedBeaconMacAddress?: string;
  profilePicturePath?: string;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  profilePicture?: {
    uri: string;
    name: string;
    type: string;
  };
  currentPassword?: string; // Required when changing email
}

export interface NotificationSettings {
  notificationsEnabled: boolean;
  vibrationEnabled: boolean;
  robotArrivalEnabled: boolean;
  robotDeliveryEnabled: boolean;
  messagesEnabled: boolean;
  statusChangesEnabled: boolean;
  robotArrivalSound: string;
  messageSound: string;
}

/**
 * Get list of all administrators - HARDCODED URL like the web fix
 */
export const getAdmins = async (): Promise<Admin[]> => {
  try {
    // Get JWT token
    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      console.error('No JWT token found');
      throw new Error('Authentication required');
    }

    console.log('🔵 Fetching admins from: http://140.245.51.90:23000/api/user/admins');

    // HARDCODED URL - same fix as web dashboard
    const response = await axios.get('http://140.245.51.90:23000/api/user/admins', {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      },
      timeout: 10000
    });

    console.log('✅ Admins loaded successfully:', response.data.length);
    return response.data;
  } catch (error: any) {
    console.error('❌ Error fetching admins:', error.response?.data || error.message);
    console.error('Status:', error.response?.status);
    throw error;
  }
};

export const userService = {
  async getProfile(): Promise<UserProfile> {
    const response = await apiGet('/user/profile');
    return response.data;
  },

  async uploadProfilePicture(imageData: { uri: string; name: string; type: string }): Promise<{ success: boolean; message: string; profilePicturePath?: string }> {
    console.log('📤 uploadProfilePicture - Uploading ONLY profile picture to /user/profile/picture');
    const formData = new FormData();
    const file: any = {
      uri: imageData.uri,
      name: imageData.name,
      type: imageData.type,
    };
    formData.append('profilePicture', file);

    const response = await apiPost('/user/profile/picture', formData);
    if (!response) {
      throw new Error('Failed to upload profile picture - no response');
    }
    return response.data;
  },

  async updateProfile(data: UpdateProfileRequest): Promise<{ success: boolean; message: string; profilePicturePath?: string }> {
    // ALWAYS send as FormData because backend expects [FromForm]
    console.log('📤 Updating profile via PUT /user/profile (FormData)');

    // Get JWT token manually (same as messageService)
    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const formData = new FormData();
    formData.append('firstName', data.firstName);
    formData.append('lastName', data.lastName);
    formData.append('email', data.email);
    if (data.phone) {
      formData.append('phone', data.phone);
    }

    // Include current password if provided (required for email change)
    if (data.currentPassword) {
      formData.append('currentPassword', data.currentPassword);
      console.log('🔒 Including current password for email change verification');
    }

    // Append file if provided
    if (data.profilePicture) {
      const file: any = {
        uri: data.profilePicture.uri,
        name: data.profilePicture.name,
        type: data.profilePicture.type,
      };
      formData.append('profilePicture', file);
      console.log('📤 Including profile picture in FormData');
    }

    console.log('📤 Sending FormData to PUT /user/profile with multipart/form-data');

    // Use axios directly (bypass apiPut wrapper)
    const response = await axios.put('http://140.245.51.90:23000/api/user/profile', formData, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'multipart/form-data',
      },
      timeout: 10000
    });

    return response.data;
  },

  async getNotificationSettings(): Promise<NotificationSettings> {
    
    const response = await apiGet('/user/notifications');
    return response.data;
  },

  async updateNotificationSettings(settings: NotificationSettings): Promise<{ success: boolean; message: string }> {
    
    const response = await apiPut('/user/notifications', settings);
    return response.data;
  },

  async deleteAccount(): Promise<{ success: boolean; message: string }> {
    
    const response = await apiDelete('/user/account');
    return response.data;
  },

  async getLaundryHistory(page: number = 1, limit: number = 10): Promise<{
    requests: any[];
    total: number;
    page: number;
    totalPages: number;
  }> {
    
    const response = await apiGet(`/user/history?page=${page}&limit=${limit}`);
    return response.data;
  },

  async getLaundryStatistics(): Promise<{
    totalRequests: number;
    completedRequests: number;
    totalWeight: number;
    totalSpent: number;
    averageWeight: number;
    favoriteTimeSlot?: string;
    lastRequest?: string;
  }> {

    const response = await apiGet('/user/statistics');
    return response.data;
  },

  async changePassword(data: { currentPassword: string; newPassword: string }): Promise<{ success: boolean; message: string }> {
    console.log('🔒 Changing password via PUT /user/password');

    // Get JWT token manually
    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      throw new Error('Authentication required');
    }

    // Send as JSON
    const response = await axios.put('http://140.245.51.90:23000/api/user/password', data, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      timeout: 10000
    });

    return response.data;
  },

  async requestEmailChange(data: { newEmail: string; currentPassword: string }): Promise<{ success: boolean; message: string; expiresAt?: string }> {
    console.log('📧 Requesting email change OTP via POST /user/request-email-change');

    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.post('http://140.245.51.90:23000/api/user/request-email-change', data, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      timeout: 10000
    });

    return response.data;
  },

  async verifyEmailChange(data: { otpCode: string }): Promise<{ success: boolean; message: string; newEmail?: string }> {
    console.log('✅ Verifying email change OTP via POST /user/verify-email-change');

    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.post('http://140.245.51.90:23000/api/user/verify-email-change', data, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json',
      },
      timeout: 10000
    });

    return response.data;
  }
};