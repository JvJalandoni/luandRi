import AsyncStorage from '@react-native-async-storage/async-storage';
import { apiPost, apiGet } from './api';
import axios from 'axios';

const API_BASE_URL = 'http://140.245.51.90:23000/api';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  customerId: string;
  customerName: string;
  expiresAt: string;
}

export interface User {
  customerId: string;
  customerName: string;
  assignedBeaconId?: number;
  roomNumber?: string;
  roomDescription?: string;
}

/**
 * Service for handling user authentication
 * Manages JWT tokens, login/logout, and user session state
 * All tokens are stored securely in AsyncStorage
 */
export const authService = {
  /**
   * Authenticates a user with username and password
   * Stores JWT token and user data in AsyncStorage on success
   * @param data - Login credentials (username and password)
   * @returns Promise with auth response containing token and user info
   * @throws Error if authentication fails or token storage fails
   */
  async login(data: LoginRequest): Promise<AuthResponse> {
    // Use direct axios call for login to avoid 401 interception
    // (401 on login means wrong credentials, not expired token)
    const response = await axios.post(`${API_BASE_URL}/auth/login`, data, {
      headers: { 'Content-Type': 'application/json' },
      timeout: 10000
    });
    const authData = response.data;

    // Store token and user data securely
    try {
      await AsyncStorage.setItem('jwt_token', authData.token);
      await AsyncStorage.setItem('user_data', JSON.stringify({
        customerId: authData.customerId,
        customerName: authData.customerName,
      }));
    } catch (error) {
      console.error('Failed to store auth data in AsyncStorage:', error);
      throw new Error('Authentication data could not be stored securely');
    }

    return authData;
  },

  /**
   * Logs out the current user
   * Clears JWT token and user data from AsyncStorage
   */
  async logout(): Promise<void> {
    try {
      await AsyncStorage.removeItem('jwt_token');
      await AsyncStorage.removeItem('user_data');
    } catch (error) {
      console.warn('Failed to clear auth data from AsyncStorage during logout:', error);
    }
  },

  /**
   * Gets the currently logged in user's data from AsyncStorage
   * @returns Promise with user data, or null if not logged in
   */
  async getCurrentUser(): Promise<User | null> {
    try {
      const userData = await AsyncStorage.getItem('user_data');
      return userData ? JSON.parse(userData) : null;
    } catch (error) {
      console.warn('Failed to retrieve user data from AsyncStorage:', error);
      return null;
    }
  },

  /**
   * Checks if user is currently logged in by validating JWT token with backend
   * Token is validated against server to ensure it's still valid
   * Returns true for network errors to support offline usage
   * @returns Promise with true if logged in with valid token, false otherwise
   */
  async isLoggedIn(): Promise<boolean> {
    try {
      const token = await AsyncStorage.getItem('jwt_token');
      if (!token) {
        return false;
      }

      // Validate token with backend - the 401 handler will clear the token
      try {
        const response = await apiGet('/auth/generate200');
        return response !== null; // If response is null, it was a 401 (handled)
      } catch (error: any) {
        // For other errors (network, etc), assume logged in for offline support
        return true;
      }
    } catch (error) {
      console.warn('Failed to check login status from AsyncStorage:', error);
      return false;
    }
  },

  /**
   * Gets the current JWT token from AsyncStorage
   * @returns Promise with JWT token string, or null if not logged in
   */
  async getToken(): Promise<string | null> {
    try {
      return await AsyncStorage.getItem('jwt_token');
    } catch (error) {
      console.warn('Failed to retrieve token from AsyncStorage:', error);
      return null;
    }
  }
};