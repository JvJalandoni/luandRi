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

  async updateProfile(data: UpdateProfileRequest): Promise<{ success: boolean; message: string; profilePicturePath?: string }> {
    // Check if we're uploading a profile picture
    if (data.profilePicture) {
      // Use FormData to support file uploads
      const formData = new FormData();
      formData.append('FirstName', data.firstName);
      formData.append('LastName', data.lastName);
      formData.append('Email', data.email);
      if (data.phone) {
        formData.append('Phone', data.phone);
      }

      // Add profile picture
      const file: any = {
        uri: data.profilePicture.uri,
        name: data.profilePicture.name,
        type: data.profilePicture.type,
      };
      formData.append('ProfilePicture', file);

      // For FormData, don't set Content-Type - axios will handle it
      const response = await apiPut('/user/profile', formData);

      if (!response) {
        throw new Error('Failed to update profile - no response');
      }
      return response.data;
    } else {
      // No profile picture - use JSON
      const payload = {
        FirstName: data.firstName,
        LastName: data.lastName,
        Email: data.email,
        Phone: data.phone,
      };

      const response = await apiPut('/user/profile', payload);
      if (!response) {
        throw new Error('Failed to update profile - no response');
      }
      return response.data;
    }
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
  }
};