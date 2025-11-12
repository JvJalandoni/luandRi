import AsyncStorage from '@react-native-async-storage/async-storage';
import axios from 'axios';

/**
 * Base URL for all API requests to the laundry robot backend server
 */
const API_BASE_URL = 'http://140.245.51.90:23000/api';

/**
 * Helper function to get authentication headers with JWT token
 * Retrieves JWT token from AsyncStorage and formats it for API requests
 * @returns Promise with headers object containing Authorization header if token exists
 */
async function getAuthHeaders() {
        try {
                const token = await AsyncStorage.getItem('jwt_token');
                console.log(`🔑 [API] Token from AsyncStorage:`, token ? `${token.substring(0, 20)}...` : 'NULL');
                if (token) {
                        return {
                                'Authorization': `Bearer ${token}`,
                                'Content-Type': 'application/json'
                        };
                }
                console.log(`🔑 [API] No token found, returning headers without Authorization`);
        } catch (error) {
                console.warn('❌ [API] Failed to retrieve JWT token from AsyncStorage:', error);
        }
        return {
                'Content-Type': 'application/json'
        };
}

/**
 * Handles 401 Unauthorized responses by clearing stored tokens and redirecting to login
 * Called automatically when API requests receive 401 status (expired/invalid token)
 * Clears JWT token and user data from AsyncStorage and navigates to login screen
 */
async function handle401() {
        console.log('🚨 handle401 CALLED - clearing tokens and redirecting');
        try {
                await AsyncStorage.removeItem('jwt_token');
                await AsyncStorage.removeItem('user_data');
                console.log('🚨 Token cleared due to 401 response');

                // Navigate to login
                const { router } = require('expo-router');
                console.log('🚨 Navigating to /auth/login');
                router.replace('/auth/login');
        } catch (error) {
                console.warn('❌ Failed to clear auth data on 401:', error);
        }
}

/**
 * Makes an authenticated GET request to the API
 * Automatically includes JWT token in Authorization header
 * Handles 401 responses by clearing tokens and redirecting to login
 * @param endpoint - API endpoint path (e.g., '/requests/active')
 * @param config - Optional axios config
 * @returns Promise with axios response, or null if 401 Unauthorized
 * @throws Error for non-401 errors (network issues, server errors, etc.)
 */
export const apiGet = async (endpoint: string, config?: any) => {
        console.log(`📡 apiGet CALLED: ${endpoint}`);
        try {
                const headers = await getAuthHeaders();
                console.log(`📡 Making GET request to: ${API_BASE_URL}${endpoint}`);
                const response = await axios.get(`${API_BASE_URL}${endpoint}`, {
                        ...config,
                        headers: { ...headers, ...(config?.headers || {}) },
                        timeout: 10000
                });
                console.log(`✅ apiGet SUCCESS: ${endpoint}`);
                return response;
        } catch (error: any) {
                console.log(`❌ apiGet ERROR: ${endpoint}, status:`, error.response?.status);
                if (error.response?.status === 401) {
                        console.log(`🚨 401 detected in apiGet: ${endpoint}`);
                        await handle401();
                        return null; // Return null for 401 errors after handling
                }
                throw error; // Only throw for non-401 errors
        }
};

/**
 * Makes an authenticated POST request to the API
 * Automatically includes JWT token in Authorization header
 * Handles 401 responses by clearing tokens and redirecting to login
 * @param endpoint - API endpoint path (e.g., '/requests')
 * @param data - Request body data to send
 * @param config - Optional axios config
 * @returns Promise with axios response, or null if 401 Unauthorized
 * @throws Error for non-401 errors
 */
export const apiPost = async (endpoint: string, data?: any, config?: any) => {
        try {
                const headers = await getAuthHeaders();
                const response = await axios.post(`${API_BASE_URL}${endpoint}`, data, {
                        ...config,
                        headers: { ...headers, ...(config?.headers || {}) },
                        timeout: 10000
                });
                return response;
        } catch (error: any) {
                if (error.response?.status === 401) {
                        await handle401();
                        return null; // Return null for 401 errors after handling
                }
                throw error; // Only throw for non-401 errors
        }
};

/**
 * Makes an authenticated PUT request to the API
 * Automatically includes JWT token in Authorization header
 * Handles 401 responses by clearing tokens and redirecting to login
 * @param endpoint - API endpoint path (e.g., '/requests/123')
 * @param data - Request body data to send
 * @param config - Optional axios config
 * @returns Promise with axios response, or null if 401 Unauthorized
 * @throws Error for non-401 errors
 */
export const apiPut = async (endpoint: string, data?: any, config?: any) => {
        try {
                const headers = await getAuthHeaders();
                const response = await axios.put(`${API_BASE_URL}${endpoint}`, data, {
                        ...config,
                        headers: { ...headers, ...(config?.headers || {}) },
                        timeout: 10000
                });
                return response;
        } catch (error: any) {
                if (error.response?.status === 401) {
                        await handle401();
                        return null; // Return null for 401 errors after handling
                }
                throw error; // Only throw for non-401 errors
        }
};

/**
 * Makes an authenticated DELETE request to the API
 * Automatically includes JWT token in Authorization header
 * Handles 401 responses by clearing tokens and redirecting to login
 * @param endpoint - API endpoint path (e.g., '/requests/123')
 * @param config - Optional axios config
 * @returns Promise with axios response, or null if 401 Unauthorized
 * @throws Error for non-401 errors
 */
export const apiDelete = async (endpoint: string, config?: any) => {
        try {
                const headers = await getAuthHeaders();
                const response = await axios.delete(`${API_BASE_URL}${endpoint}`, {
                        ...config,
                        headers: { ...headers, ...(config?.headers || {}) },
                        timeout: 10000
                });
                return response;
        } catch (error: any) {
                if (error.response?.status === 401) {
                        await handle401();
                        return null; // Return null for 401 errors after handling
                }
                throw error; // Only throw for non-401 errors
        }
};

/**
 * Basic axios instance without authentication or interceptors
 * For backward compatibility with code that doesn't need auth
 * Prefer using apiGet, apiPost, apiPut, apiDelete for authenticated requests
 */
export const api = axios.create({
        baseURL: API_BASE_URL,
        timeout: 10000,
        headers: {
                'Content-Type': 'application/json',
        },
});