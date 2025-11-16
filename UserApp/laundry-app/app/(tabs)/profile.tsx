import { useFocusEffect, useRouter } from 'expo-router';
import React, { useCallback, useEffect, useState } from 'react';
import {
        ScrollView,
        StyleSheet,
        Text,
        TextInput,
        TouchableOpacity,
        View,
        Image,
        ActivityIndicator,
        Modal,
        FlatList,
        Linking,
} from 'react-native';
import * as ImagePicker from 'expo-image-picker';
import { useCustomAlert } from '../../components/CustomAlert';
import { ThemedText } from '../../components/ThemedText';
import { ThemedView } from '../../components/ThemedView';
import { useAuth } from '../../contexts/AuthContext';
import { useThemeColor } from '../../hooks/useThemeColor';
import { userService } from '../../services/userService';
import { apiGet } from '../../services/api';

interface AdminContact {
        id: string;
        firstName: string;
        lastName: string;
        fullName: string;
        email: string;
        phoneNumber: string | null;
        isActive: boolean;
}

/**
 * Profile Screen - User account management and settings
 * Features:
 * - View and edit user profile (name, email, phone)
 * - Display assigned room and beacon information
 * - Logout functionality
 * - Auto-refresh when screen gains focus
 * - Save profile changes to server
 * - Pull user data from auth context as fallback
 *
 * @returns React component for user profile management
 */
export default function ProfileScreen() {
        const router = useRouter();
        const { user, logout, refreshProfile } = useAuth();
        const { showAlert, AlertComponent } = useCustomAlert();
        const [isEditing, setIsEditing] = useState(false);
        const [firstName, setFirstName] = useState('');
        const [lastName, setLastName] = useState('');
        const [email, setEmail] = useState('');
        const [phone, setPhone] = useState('');
        const [isLoading, setIsLoading] = useState(false);
        const [profilePicturePath, setProfilePicturePath] = useState<string | null>(null);
        const [isUploadingImage, setIsUploadingImage] = useState(false);
        const [originalEmail, setOriginalEmail] = useState(''); // Track original email
        const [showPasswordModal, setShowPasswordModal] = useState(false);
        const [currentPassword, setCurrentPassword] = useState('');

        // Change Password modal state
        const [showChangePasswordModal, setShowChangePasswordModal] = useState(false);
        const [changePasswordData, setChangePasswordData] = useState({
                currentPassword: '',
                newPassword: '',
                confirmPassword: ''
        });

        // Contact Support state
        const [showAdminListModal, setShowAdminListModal] = useState(false);
        const [showAdminDetailModal, setShowAdminDetailModal] = useState(false);
        const [adminList, setAdminList] = useState<AdminContact[]>([]);
        const [selectedAdmin, setSelectedAdmin] = useState<AdminContact | null>(null);
        const [isLoadingAdmins, setIsLoadingAdmins] = useState(false);

        const backgroundColor = useThemeColor({}, 'background');
        const textColor = useThemeColor({}, 'text');
        const primaryColor = useThemeColor({}, 'primary');
        const secondaryColor = useThemeColor({}, 'secondary');
        const cardColor = useThemeColor({}, 'card');
        const borderColor = useThemeColor({}, 'border');
        const mutedColor = useThemeColor({}, 'muted');
        const dangerColor = useThemeColor({}, 'danger');

        useEffect(() => {
                loadProfile();
        }, []);

        useFocusEffect(
                useCallback(() => {
                        refreshProfile();
                        loadProfile();
                }, []) // Remove refreshProfile from dependencies to prevent infinite loop
        );

        /**
         * Loads user profile data from server
         * Falls back to auth context user data if profile fetch fails
         * Populates form fields with current user information
         */
        const loadProfile = async () => {
                console.log('📥 loadProfile CALLED');
                console.log('👤 User context:', user);

                try {
                        const profile = await userService.getProfile();
                        console.log('📊 Profile from API:', profile);

                        // Always use fallbacks if profile fields are empty
                        const [first, ...lastParts] = user?.customerName?.split(' ') || ['', ''];

                        const finalFirstName = profile.firstName || first || '';
                        const finalLastName = profile.lastName || lastParts.join(' ') || '';
                        const finalEmail = profile.email || user?.email || '';
                        const finalPhone = profile.phone || user?.phone || '';

                        console.log('✅ Setting state values:', { finalFirstName, finalLastName, finalEmail, finalPhone });

                        setFirstName(finalFirstName);
                        setLastName(finalLastName);
                        setEmail(finalEmail);
                        setOriginalEmail(finalEmail); // Track original email
                        setPhone(finalPhone);
                        setProfilePicturePath(profile.profilePicturePath || null);
                } catch (error) {
                        console.error('❌ loadProfile error:', error);
                        // If profile doesn't exist, use user data from auth
                        if (user) {
                                const [first, ...lastParts] = user.customerName.split(' ');
                                console.log('🔄 Using fallback from user context:', { first, last: lastParts.join(' ') });
                                setFirstName(first || '');
                                setLastName(lastParts.join(' ') || '');
                                setEmail(user.email || '');
                                setOriginalEmail(user.email || ''); // Track original email
                                setPhone(user.phone || '');
                                setProfilePicturePath(user.profilePicturePath || null);
                        }
                }
        };

        const handlePickImage = async () => {
                console.log('🖼️ handlePickImage CALLED');
                console.log('📝 Current state values:', { firstName, lastName, email, phone });
                console.log('👤 User from context:', user);

                try {
                        const permissionResult = await ImagePicker.requestMediaLibraryPermissionsAsync();

                        if (permissionResult.granted === false) {
                                showAlert('Permission Required', 'Please allow access to your photo library to upload a profile picture');
                                return;
                        }

                        const result = await ImagePicker.launchImageLibraryAsync({
                                mediaTypes: ['images'],
                                allowsEditing: true,
                                aspect: [1, 1],
                                quality: 0.8,
                        });

                        if (!result.canceled && result.assets[0]) {
                                const asset = result.assets[0];
                                const fileName = asset.uri.split('/').pop() || 'profile.jpg';
                                const type = `image/${fileName.split('.').pop() || 'jpeg'}`;

                                const imageData = {
                                        uri: asset.uri,
                                        name: fileName,
                                        type: type,
                                };

                                console.log('📤 Uploading profile picture WITH current profile data (same as admin)');

                                setIsUploadingImage(true);
                                try {
                                        // Use updateProfile with JUST the profile picture + existing data
                                        // This matches how the admin profile works
                                        const result = await userService.updateProfile({
                                                firstName: firstName || user?.customerName?.split(' ')[0] || '',
                                                lastName: lastName || user?.customerName?.split(' ').slice(1).join(' ') || '',
                                                email: email || user?.email || '',
                                                phone: phone || undefined,
                                                profilePicture: imageData
                                        });

                                        console.log('✅ Upload successful:', result);

                                        if (result.profilePicturePath) {
                                                setProfilePicturePath(result.profilePicturePath);
                                        }

                                        await refreshProfile();
                                        showAlert('Success', 'Profile picture updated successfully');
                                } catch (error: any) {
                                        console.error('❌ Upload failed:', error);
                                        showAlert('Error', error.response?.data?.message || 'Failed to upload profile picture');
                                } finally {
                                        setIsUploadingImage(false);
                                }
                        }
                } catch (error) {
                        console.error('❌ Image picker error:', error);
                        showAlert('Error', 'Failed to pick image');
                }
        };

        const handleSave = async () => {
                // Get fallback values from user context
                const [first, ...lastParts] = user?.customerName?.split(' ') || ['User', ''];

                const finalFirstName = firstName.trim() || first || 'User';
                const finalLastName = lastName.trim() || lastParts.join(' ') || '';
                const finalEmail = email.trim() || user?.email || 'noemail@example.com';

                // Check if email has changed - require password confirmation
                if (finalEmail.toLowerCase() !== originalEmail.toLowerCase()) {
                        console.log('🔒 Email changed - showing password confirmation');
                        setShowPasswordModal(true);
                        return;
                }

                // No email change - proceed normally
                await saveProfile();
        };

        const saveProfile = async (password?: string) => {
                const [first, ...lastParts] = user?.customerName?.split(' ') || ['User', ''];

                const finalFirstName = firstName.trim() || first || 'User';
                const finalLastName = lastName.trim() || lastParts.join(' ') || '';
                const finalEmail = email.trim() || user?.email || 'noemail@example.com';

                setIsLoading(true);
                try {
                        const result = await userService.updateProfile({
                                firstName: finalFirstName,
                                lastName: finalLastName,
                                email: finalEmail,
                                phone: phone.trim() || undefined,
                                currentPassword: password, // Send password if email changed
                        });

                        await refreshProfile(); // Refresh to show updated data
                        showAlert('Success', 'Profile updated successfully');
                        setIsEditing(false);
                        setShowPasswordModal(false);
                        setCurrentPassword('');
                } catch (error: any) {
                        const errorMsg = error.response?.data?.message || 'Failed to update profile';
                        showAlert('Error', errorMsg);

                        // If password was wrong, keep modal open
                        if (errorMsg.toLowerCase().includes('password')) {
                                setCurrentPassword(''); // Clear password field
                        } else {
                                setShowPasswordModal(false);
                        }
                } finally {
                        setIsLoading(false);
                }
        };

        const handleCancel = () => {
                setIsEditing(false);
                loadProfile(); // Reset to original values
        };

        /**
         * Handles password change
         * Validates new password and confirm password match
         * Sends change password request to backend with audit logging
         */
        const handleChangePassword = async () => {
                const { currentPassword: currPass, newPassword, confirmPassword } = changePasswordData;

                // Validation
                if (!currPass.trim() || !newPassword.trim() || !confirmPassword.trim()) {
                        showAlert('Error', 'All password fields are required');
                        return;
                }

                if (newPassword !== confirmPassword) {
                        showAlert('Error', 'New password and confirm password do not match');
                        return;
                }

                if (newPassword.length < 6) {
                        showAlert('Error', 'New password must be at least 6 characters long');
                        return;
                }

                setIsLoading(true);
                try {
                        const result = await userService.changePassword({
                                currentPassword: currPass,
                                newPassword: newPassword
                        });

                        showAlert('Success', result.message || 'Password changed successfully');
                        setShowChangePasswordModal(false);
                        setChangePasswordData({ currentPassword: '', newPassword: '', confirmPassword: '' });
                } catch (error: any) {
                        const errorMsg = error.response?.data?.message || 'Failed to change password';
                        showAlert('Error', errorMsg);

                        // If current password was wrong, clear only that field
                        if (errorMsg.toLowerCase().includes('current password')) {
                                setChangePasswordData(prev => ({ ...prev, currentPassword: '' }));
                        }
                } finally {
                        setIsLoading(false);
                }
        };

        const handleLogout = () => {
                showAlert(
                        'Logout',
                        'Are you sure you want to logout?',
                        [
                                { text: 'Cancel', style: 'cancel' },
                                {
                                        text: 'Logout',
                                        style: 'destructive',
                                        onPress: async () => {
                                                try {
                                                        await logout();
                                                } catch (error) {
                                                        console.error('Logout failed, but should have been handled:', error);
                                                        // If logout completely fails, show alert but don't prevent user from trying again
                                                        showAlert('Error', 'Logout encountered an issue, but auth data was cleared. Please restart the app if needed.');
                                                }
                                        }
                                },
                        ]
                );
        };

        /**
         * Fetches list of admin contacts from server
         * Caches the list for offline access
         */
        const fetchAdminContacts = async () => {
                setIsLoadingAdmins(true);
                try {
                        const response = await apiGet('/user/admins');
                        const admins = response.data as AdminContact[];
                        setAdminList(admins);
                        setShowAdminListModal(true);
                } catch (error: any) {
                        console.error('Failed to fetch admin contacts:', error);
                        showAlert('Error', 'Failed to load support contacts. Please check your internet connection.');
                } finally {
                        setIsLoadingAdmins(false);
                }
        };

        /**
         * Handles selecting an admin from the list
         */
        const handleSelectAdmin = (admin: AdminContact) => {
                setSelectedAdmin(admin);
                setShowAdminListModal(false);
                setShowAdminDetailModal(true);
        };

        /**
         * Opens phone dialer with admin's phone number
         */
        const handleCallAdmin = (phoneNumber: string) => {
                Linking.openURL(`tel:${phoneNumber}`);
        };

        /**
         * Opens email client with admin's email
         */
        const handleEmailAdmin = (email: string) => {
                Linking.openURL(`mailto:${email}`);
        };


        return (
                <ThemedView style={styles.container}>
                        <ScrollView style={styles.scrollContainer}>
                                <View style={styles.header}>
                                        <TouchableOpacity
                                                onPress={handlePickImage}
                                                style={styles.avatarContainer}
                                        >
                                                <View style={[styles.avatar, { backgroundColor: primaryColor }]}>
                                                        {profilePicturePath ? (
                                                                <Image
                                                                        source={{ uri: `http://140.245.51.90:23000${profilePicturePath}` }}
                                                                        style={styles.avatarImage}
                                                                />
                                                        ) : (
                                                                <Text style={styles.avatarText}>
                                                                        {firstName.charAt(0)}{lastName.charAt(0)}
                                                                </Text>
                                                        )}
                                                        {isUploadingImage && (
                                                                <View style={[styles.avatarImage, { position: 'absolute', backgroundColor: 'rgba(0,0,0,0.5)', justifyContent: 'center', alignItems: 'center' }]}>
                                                                        <ActivityIndicator size="large" color="#ffffff" />
                                                                </View>
                                                        )}
                                                </View>
                                                <View style={[styles.cameraIcon, { backgroundColor: primaryColor }]}>
                                                        <Text style={styles.cameraIconText}>📷</Text>
                                                </View>
                                        </TouchableOpacity>
                                        <ThemedText style={styles.userName}>{user?.customerName || 'User'}</ThemedText>
                                        <ThemedText style={[styles.userId, { color: mutedColor }]}>ID: {user?.customerId || 'N/A'}</ThemedText>
                                </View>

                                <View style={[styles.section, { backgroundColor: cardColor }]}>
                                        <View style={styles.sectionHeader}>
                                                <ThemedText style={styles.sectionTitle}>Personal Information</ThemedText>
                                                {!isEditing && (
                                                        <TouchableOpacity onPress={() => setIsEditing(true)}>
                                                                <ThemedText style={[styles.editButton, { color: primaryColor }]}>Edit</ThemedText>
                                                        </TouchableOpacity>
                                                )}
                                        </View>

                                        <View style={styles.form}>
                                                <View style={styles.inputGroup}>
                                                        <ThemedText style={[styles.label, { color: textColor }]}>First Name</ThemedText>
                                                        <TextInput
                                                                style={[
                                                                        styles.input,
                                                                        { backgroundColor: isEditing ? cardColor : backgroundColor, borderColor: borderColor, color: textColor },
                                                                        !isEditing && { backgroundColor: backgroundColor }
                                                                ]}
                                                                value={firstName}
                                                                onChangeText={setFirstName}
                                                                placeholder="Enter your first name"
                                                                placeholderTextColor={mutedColor}
                                                                editable={isEditing}
                                                                autoCapitalize="words"
                                                        />
                                                </View>

                                                <View style={styles.inputGroup}>
                                                        <ThemedText style={[styles.label, { color: textColor }]}>Last Name</ThemedText>
                                                        <TextInput
                                                                style={[
                                                                        styles.input,
                                                                        { backgroundColor: isEditing ? cardColor : backgroundColor, borderColor: borderColor, color: textColor },
                                                                        !isEditing && { backgroundColor: backgroundColor }
                                                                ]}
                                                                value={lastName}
                                                                onChangeText={setLastName}
                                                                placeholder="Enter your last name"
                                                                placeholderTextColor={mutedColor}
                                                                editable={isEditing}
                                                                autoCapitalize="words"
                                                        />
                                                </View>

                                                <View style={styles.inputGroup}>
                                                        <ThemedText style={[styles.label, { color: textColor }]}>Email</ThemedText>
                                                        <TextInput
                                                                style={[
                                                                        styles.input,
                                                                        { backgroundColor: isEditing ? cardColor : backgroundColor, borderColor: borderColor, color: textColor },
                                                                        !isEditing && { backgroundColor: backgroundColor }
                                                                ]}
                                                                value={email}
                                                                onChangeText={setEmail}
                                                                placeholder="Enter your email"
                                                                placeholderTextColor={mutedColor}
                                                                editable={isEditing}
                                                                keyboardType="email-address"
                                                                autoCapitalize="none"
                                                        />
                                                </View>

                                                <View style={styles.inputGroup}>
                                                        <ThemedText style={[styles.label, { color: textColor }]}>Phone Number</ThemedText>
                                                        <TextInput
                                                                style={[
                                                                        styles.input,
                                                                        { backgroundColor: isEditing ? cardColor : backgroundColor, borderColor: borderColor, color: textColor },
                                                                        !isEditing && { backgroundColor: backgroundColor }
                                                                ]}
                                                                value={phone}
                                                                onChangeText={setPhone}
                                                                placeholder="Enter your phone number"
                                                                placeholderTextColor={mutedColor}
                                                                editable={isEditing}
                                                                keyboardType="phone-pad"
                                                        />
                                                </View>

                                                <View style={styles.inputGroup}>
                                                        <ThemedText style={[styles.label, { color: textColor }]}>Assigned Room</ThemedText>
                                                        <View style={[styles.input, styles.readOnlyInput, { backgroundColor: backgroundColor, borderColor: borderColor }]}>
                                                                <ThemedText style={[styles.readOnlyText, { color: user?.roomName ? textColor : mutedColor }]}>
                                                                        {user?.roomName ? `📍 ${user.roomName}` : 'No room assigned'}
                                                                </ThemedText>
                                                        </View>
                                                        {user?.roomDescription && (
                                                                <ThemedText style={[styles.roomDescription, { color: mutedColor }]}>
                                                                        {user.roomDescription}
                                                                </ThemedText>
                                                        )}
                                                </View>

                                                {isEditing && (
                                                        <View style={styles.buttonRow}>
                                                                <TouchableOpacity
                                                                        style={[styles.button, styles.cancelButton, { backgroundColor: backgroundColor, borderColor: borderColor }]}
                                                                        onPress={handleCancel}
                                                                >
                                                                        <ThemedText style={[styles.cancelButtonText, { color: mutedColor }]}>Cancel</ThemedText>
                                                                </TouchableOpacity>
                                                                <TouchableOpacity
                                                                        style={[styles.button, { backgroundColor: isLoading ? mutedColor : primaryColor }]}
                                                                        onPress={handleSave}
                                                                        disabled={isLoading}
                                                                >
                                                                        <Text style={styles.saveButtonText}>
                                                                                {isLoading ? 'Saving...' : 'Save'}
                                                                        </Text>
                                                                </TouchableOpacity>
                                                        </View>
                                                )}
                                        </View>
                                </View>

                                {/* Security Section */}
                                <View style={[styles.section, { backgroundColor: cardColor }]}>
                                        <ThemedText style={styles.sectionTitle}>Security</ThemedText>
                                        <TouchableOpacity
                                                style={[styles.changePasswordButton, { backgroundColor: secondaryColor }]}
                                                onPress={() => setShowChangePasswordModal(true)}
                                        >
                                                <Text style={styles.changePasswordButtonText}>Change Password</Text>
                                        </TouchableOpacity>
                                </View>

                                {/* Contact Support Section */}
                                <View style={[styles.section, { backgroundColor: cardColor }]}>
                                        <ThemedText style={styles.sectionTitle}>Support</ThemedText>
                                        <TouchableOpacity
                                                style={[styles.contactSupportButton, { backgroundColor: primaryColor }]}
                                                onPress={fetchAdminContacts}
                                                disabled={isLoadingAdmins}
                                        >
                                                {isLoadingAdmins ? (
                                                        <ActivityIndicator color="#fff" />
                                                ) : (
                                                        <Text style={styles.contactSupportButtonText}>Contact Support</Text>
                                                )}
                                        </TouchableOpacity>
                                        <ThemedText style={[styles.supportDescription, { color: mutedColor }]}>
                                                Get admin contact information for assistance
                                        </ThemedText>
                                </View>

                                <View style={[styles.section, { backgroundColor: cardColor }]}>
                                        <TouchableOpacity style={[styles.logoutButton, { backgroundColor: dangerColor }]} onPress={handleLogout}>
                                                <Text style={styles.logoutButtonText}>Logout</Text>
                                        </TouchableOpacity>
                                </View>
                        </ScrollView>

                        {/* Password Confirmation Modal */}
                        <Modal
                                visible={showPasswordModal}
                                transparent={true}
                                animationType="fade"
                                onRequestClose={() => {
                                        setShowPasswordModal(false);
                                        setCurrentPassword('');
                                }}
                        >
                                <View style={styles.modalOverlay}>
                                        <View style={[styles.modalContent, { backgroundColor: cardColor }]}>
                                                <Text style={[styles.modalTitle, { color: textColor }]}>
                                                        Confirm Password
                                                </Text>
                                                <Text style={[styles.modalDescription, { color: mutedColor }]}>
                                                        You are changing your email address. Please enter your current password to confirm.
                                                </Text>

                                                <TextInput
                                                        style={[styles.modalInput, { borderColor, color: textColor }]}
                                                        placeholder="Current Password"
                                                        placeholderTextColor={mutedColor}
                                                        secureTextEntry
                                                        value={currentPassword}
                                                        onChangeText={setCurrentPassword}
                                                        autoFocus
                                                />

                                                <View style={styles.modalButtons}>
                                                        <TouchableOpacity
                                                                style={[styles.modalButton, styles.cancelButton, { backgroundColor: mutedColor }]}
                                                                onPress={() => {
                                                                        setShowPasswordModal(false);
                                                                        setCurrentPassword('');
                                                                }}
                                                                disabled={isLoading}
                                                        >
                                                                <Text style={styles.modalButtonText}>Cancel</Text>
                                                        </TouchableOpacity>

                                                        <TouchableOpacity
                                                                style={[styles.modalButton, { backgroundColor: primaryColor }]}
                                                                onPress={() => saveProfile(currentPassword)}
                                                                disabled={isLoading || !currentPassword.trim()}
                                                        >
                                                                {isLoading ? (
                                                                        <ActivityIndicator color="#fff" />
                                                                ) : (
                                                                        <Text style={styles.modalButtonText}>Confirm</Text>
                                                                )}
                                                        </TouchableOpacity>
                                                </View>
                                        </View>
                                </View>
                        </Modal>

                        {/* Change Password Modal */}
                        <Modal
                                visible={showChangePasswordModal}
                                transparent={true}
                                animationType="fade"
                                onRequestClose={() => {
                                        setShowChangePasswordModal(false);
                                        setChangePasswordData({ currentPassword: '', newPassword: '', confirmPassword: '' });
                                }}
                        >
                                <View style={styles.modalOverlay}>
                                        <View style={[styles.modalContent, { backgroundColor: cardColor }]}>
                                                <Text style={[styles.modalTitle, { color: textColor }]}>
                                                        Change Password
                                                </Text>
                                                <Text style={[styles.modalDescription, { color: mutedColor }]}>
                                                        Enter your current password and choose a new password.
                                                </Text>

                                                <TextInput
                                                        style={[styles.modalInput, { borderColor, color: textColor }]}
                                                        placeholder="Current Password"
                                                        placeholderTextColor={mutedColor}
                                                        secureTextEntry
                                                        value={changePasswordData.currentPassword}
                                                        onChangeText={(text) => setChangePasswordData(prev => ({ ...prev, currentPassword: text }))}
                                                        autoFocus
                                                />

                                                <TextInput
                                                        style={[styles.modalInput, { borderColor, color: textColor }]}
                                                        placeholder="New Password"
                                                        placeholderTextColor={mutedColor}
                                                        secureTextEntry
                                                        value={changePasswordData.newPassword}
                                                        onChangeText={(text) => setChangePasswordData(prev => ({ ...prev, newPassword: text }))}
                                                />

                                                <TextInput
                                                        style={[styles.modalInput, { borderColor, color: textColor }]}
                                                        placeholder="Confirm New Password"
                                                        placeholderTextColor={mutedColor}
                                                        secureTextEntry
                                                        value={changePasswordData.confirmPassword}
                                                        onChangeText={(text) => setChangePasswordData(prev => ({ ...prev, confirmPassword: text }))}
                                                />

                                                <View style={styles.modalButtons}>
                                                        <TouchableOpacity
                                                                style={[styles.modalButton, styles.cancelButton, { backgroundColor: mutedColor }]}
                                                                onPress={() => {
                                                                        setShowChangePasswordModal(false);
                                                                        setChangePasswordData({ currentPassword: '', newPassword: '', confirmPassword: '' });
                                                                }}
                                                                disabled={isLoading}
                                                        >
                                                                <Text style={styles.modalButtonText}>Cancel</Text>
                                                        </TouchableOpacity>

                                                        <TouchableOpacity
                                                                style={[styles.modalButton, { backgroundColor: primaryColor }]}
                                                                onPress={handleChangePassword}
                                                                disabled={isLoading || !changePasswordData.currentPassword.trim() || !changePasswordData.newPassword.trim() || !changePasswordData.confirmPassword.trim()}
                                                        >
                                                                {isLoading ? (
                                                                        <ActivityIndicator color="#fff" />
                                                                ) : (
                                                                        <Text style={styles.modalButtonText}>Change Password</Text>
                                                                )}
                                                        </TouchableOpacity>
                                                </View>
                                        </View>
                                </View>
                        </Modal>

                        {/* Admin List Modal */}
                        <Modal
                                visible={showAdminListModal}
                                transparent={true}
                                animationType="fade"
                                onRequestClose={() => setShowAdminListModal(false)}
                        >
                                <View style={styles.modalOverlay}>
                                        <View style={[styles.modalContent, { backgroundColor: cardColor, maxHeight: '70%' }]}>
                                                <Text style={[styles.modalTitle, { color: textColor }]}>
                                                        Support Contacts
                                                </Text>
                                                <Text style={[styles.modalDescription, { color: mutedColor }]}>
                                                        Select an administrator to view their contact information.
                                                </Text>

                                                {adminList.length === 0 ? (
                                                        <Text style={[styles.emptyListText, { color: mutedColor }]}>
                                                                No administrators available
                                                        </Text>
                                                ) : (
                                                        <FlatList
                                                                data={adminList}
                                                                keyExtractor={(item) => item.id}
                                                                style={styles.adminList}
                                                                renderItem={({ item }) => (
                                                                        <TouchableOpacity
                                                                                style={[styles.adminListItem, { borderColor: borderColor }]}
                                                                                onPress={() => handleSelectAdmin(item)}
                                                                        >
                                                                                <View style={styles.adminListItemContent}>
                                                                                        <View style={[styles.adminAvatar, { backgroundColor: primaryColor }]}>
                                                                                                <Text style={styles.adminAvatarText}>
                                                                                                        {item.firstName.charAt(0)}{item.lastName.charAt(0)}
                                                                                                </Text>
                                                                                        </View>
                                                                                        <View style={styles.adminInfo}>
                                                                                                <Text style={[styles.adminName, { color: textColor }]}>
                                                                                                        {item.fullName}
                                                                                                </Text>
                                                                                                <Text style={[styles.adminEmail, { color: mutedColor }]}>
                                                                                                        {item.email}
                                                                                                </Text>
                                                                                        </View>
                                                                                        {!item.isActive && (
                                                                                                <View style={[styles.inactiveBadge, { backgroundColor: mutedColor }]}>
                                                                                                        <Text style={styles.inactiveBadgeText}>Inactive</Text>
                                                                                                </View>
                                                                                        )}
                                                                                </View>
                                                                                <Text style={[styles.adminListArrow, { color: mutedColor }]}>›</Text>
                                                                        </TouchableOpacity>
                                                                )}
                                                        />
                                                )}

                                                <TouchableOpacity
                                                        style={[styles.closeButton, { backgroundColor: mutedColor }]}
                                                        onPress={() => setShowAdminListModal(false)}
                                                >
                                                        <Text style={styles.closeButtonText}>Close</Text>
                                                </TouchableOpacity>
                                        </View>
                                </View>
                        </Modal>

                        {/* Admin Detail Modal */}
                        <Modal
                                visible={showAdminDetailModal}
                                transparent={true}
                                animationType="fade"
                                onRequestClose={() => setShowAdminDetailModal(false)}
                        >
                                <View style={styles.modalOverlay}>
                                        <View style={[styles.modalContent, { backgroundColor: cardColor }]}>
                                                {selectedAdmin && (
                                                        <>
                                                                <View style={styles.adminDetailHeader}>
                                                                        <View style={[styles.adminDetailAvatar, { backgroundColor: primaryColor }]}>
                                                                                <Text style={styles.adminDetailAvatarText}>
                                                                                        {selectedAdmin.firstName.charAt(0)}{selectedAdmin.lastName.charAt(0)}
                                                                                </Text>
                                                                        </View>
                                                                        <Text style={[styles.adminDetailName, { color: textColor }]}>
                                                                                {selectedAdmin.fullName}
                                                                        </Text>
                                                                        {!selectedAdmin.isActive && (
                                                                                <View style={[styles.inactiveBadge, { backgroundColor: mutedColor }]}>
                                                                                        <Text style={styles.inactiveBadgeText}>Inactive</Text>
                                                                                </View>
                                                                        )}
                                                                </View>

                                                                <View style={styles.contactInfoSection}>
                                                                        <Text style={[styles.contactLabel, { color: mutedColor }]}>Email</Text>
                                                                        <TouchableOpacity
                                                                                style={[styles.contactButton, { backgroundColor: primaryColor }]}
                                                                                onPress={() => handleEmailAdmin(selectedAdmin.email)}
                                                                        >
                                                                                <Text style={styles.contactButtonIcon}>📧</Text>
                                                                                <Text style={styles.contactButtonText}>{selectedAdmin.email}</Text>
                                                                        </TouchableOpacity>
                                                                </View>

                                                                <View style={styles.contactInfoSection}>
                                                                        <Text style={[styles.contactLabel, { color: mutedColor }]}>Phone</Text>
                                                                        {selectedAdmin.phoneNumber ? (
                                                                                <TouchableOpacity
                                                                                        style={[styles.contactButton, { backgroundColor: secondaryColor }]}
                                                                                        onPress={() => handleCallAdmin(selectedAdmin.phoneNumber!)}
                                                                                >
                                                                                        <Text style={styles.contactButtonIcon}>📞</Text>
                                                                                        <Text style={styles.contactButtonText}>{selectedAdmin.phoneNumber}</Text>
                                                                                </TouchableOpacity>
                                                                        ) : (
                                                                                <Text style={[styles.noPhoneText, { color: mutedColor }]}>
                                                                                        No phone number available
                                                                                </Text>
                                                                        )}
                                                                </View>
                                                        </>
                                                )}

                                                <View style={styles.adminDetailButtons}>
                                                        <TouchableOpacity
                                                                style={[styles.backButton, { backgroundColor: mutedColor }]}
                                                                onPress={() => {
                                                                        setShowAdminDetailModal(false);
                                                                        setShowAdminListModal(true);
                                                                }}
                                                        >
                                                                <Text style={styles.backButtonText}>Back to List</Text>
                                                        </TouchableOpacity>
                                                        <TouchableOpacity
                                                                style={[styles.closeButton, { backgroundColor: dangerColor }]}
                                                                onPress={() => setShowAdminDetailModal(false)}
                                                        >
                                                                <Text style={styles.closeButtonText}>Close</Text>
                                                        </TouchableOpacity>
                                                </View>
                                        </View>
                                </View>
                        </Modal>

                        <AlertComponent />
                </ThemedView>
        );
}

const styles = StyleSheet.create({
        container: {
                flex: 1,
        },
        scrollContainer: {
                flex: 1,
        },
        header: {
                alignItems: 'center',
                padding: 32,
                paddingTop: 60,
        },
        avatarContainer: {
                position: 'relative',
                marginBottom: 16,
        },
        avatar: {
                width: 80,
                height: 80,
                borderRadius: 40,
                alignItems: 'center',
                justifyContent: 'center',
                overflow: 'hidden',
        },
        avatarImage: {
                width: '100%',
                height: '100%',
        },
        avatarText: {
                fontSize: 28,
                fontWeight: 'bold',
                color: '#ffffff',
        },
        cameraIcon: {
                position: 'absolute',
                bottom: 0,
                right: 0,
                width: 28,
                height: 28,
                borderRadius: 14,
                alignItems: 'center',
                justifyContent: 'center',
                borderWidth: 2,
                borderColor: '#ffffff',
        },
        cameraIconText: {
                fontSize: 14,
        },
        imageSelected: {
                fontSize: 12,
                marginTop: 4,
                fontWeight: '500',
        },
        userName: {
                fontSize: 24,
                fontWeight: 'bold',
                marginBottom: 4,
        },
        userId: {
                fontSize: 14,
        },
        section: {
                marginHorizontal: 16,
                marginBottom: 16,
                borderRadius: 12,
                padding: 20,
        },
        sectionHeader: {
                flexDirection: 'row',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: 20,
        },
        sectionTitle: {
                fontSize: 18,
                fontWeight: '600',
        },
        editButton: {
                fontSize: 16,
                fontWeight: '600',
        },
        form: {
                gap: 16,
        },
        inputGroup: {
                gap: 8,
        },
        label: {
                fontSize: 16,
                fontWeight: '600',
        },
        input: {
                borderWidth: 1,
                borderRadius: 8,
                padding: 16,
                fontSize: 16,
        },
        readOnlyInput: {
                justifyContent: 'center',
        },
        readOnlyText: {
                fontSize: 16,
        },
        roomDescription: {
                fontSize: 14,
                marginTop: 4,
                fontStyle: 'italic',
        },
        buttonRow: {
                flexDirection: 'row',
                gap: 12,
                marginTop: 8,
        },
        button: {
                flex: 1,
                borderRadius: 8,
                padding: 14,
                alignItems: 'center',
        },
        cancelButton: {
                borderWidth: 1,
        },
        cancelButtonText: {
                fontSize: 16,
                fontWeight: '600',
        },
        saveButtonText: {
                color: '#ffffff',
                fontSize: 16,
                fontWeight: '600',
        },
        menuItem: {
                flexDirection: 'row',
                justifyContent: 'space-between',
                alignItems: 'center',
                paddingVertical: 16,
                borderBottomWidth: 1,
        },
        menuItemText: {
                fontSize: 16,
        },
        menuItemArrow: {
                fontSize: 18,
        },
        changePasswordButton: {
                borderRadius: 8,
                padding: 16,
                alignItems: 'center',
                marginTop: 12,
        },
        changePasswordButtonText: {
                color: '#ffffff',
                fontSize: 16,
                fontWeight: '600',
        },
        contactSupportButton: {
                borderRadius: 8,
                padding: 16,
                alignItems: 'center',
                marginTop: 12,
        },
        contactSupportButtonText: {
                color: '#ffffff',
                fontSize: 16,
                fontWeight: '600',
        },
        supportDescription: {
                fontSize: 12,
                marginTop: 8,
                textAlign: 'center',
        },
        logoutButton: {
                borderRadius: 8,
                padding: 16,
                alignItems: 'center',
        },
        logoutButtonText: {
                color: '#ffffff',
                fontSize: 16,
                fontWeight: '600',
        },
        modalOverlay: {
                flex: 1,
                backgroundColor: 'rgba(0, 0, 0, 0.5)',
                justifyContent: 'center',
                alignItems: 'center',
        },
        modalContent: {
                width: '85%',
                borderRadius: 12,
                padding: 24,
                shadowColor: '#000',
                shadowOffset: { width: 0, height: 2 },
                shadowOpacity: 0.25,
                shadowRadius: 4,
                elevation: 5,
        },
        modalTitle: {
                fontSize: 20,
                fontWeight: '700',
                marginBottom: 12,
        },
        modalDescription: {
                fontSize: 14,
                marginBottom: 20,
                lineHeight: 20,
        },
        modalInput: {
                borderWidth: 1,
                borderRadius: 8,
                padding: 12,
                fontSize: 16,
                marginBottom: 20,
        },
        modalButtons: {
                flexDirection: 'row',
                justifyContent: 'space-between',
                gap: 12,
        },
        modalButton: {
                flex: 1,
                padding: 14,
                borderRadius: 8,
                alignItems: 'center',
        },
        cancelButton: {
                opacity: 0.7,
        },
        modalButtonText: {
                color: '#fff',
                fontSize: 16,
                fontWeight: '600',
        },
        // Admin List Modal Styles
        emptyListText: {
                textAlign: 'center',
                fontSize: 14,
                marginVertical: 20,
        },
        adminList: {
                maxHeight: 300,
                marginBottom: 16,
        },
        adminListItem: {
                flexDirection: 'row',
                alignItems: 'center',
                justifyContent: 'space-between',
                paddingVertical: 12,
                borderBottomWidth: 1,
        },
        adminListItemContent: {
                flexDirection: 'row',
                alignItems: 'center',
                flex: 1,
        },
        adminAvatar: {
                width: 40,
                height: 40,
                borderRadius: 20,
                alignItems: 'center',
                justifyContent: 'center',
                marginRight: 12,
        },
        adminAvatarText: {
                color: '#ffffff',
                fontSize: 14,
                fontWeight: '600',
        },
        adminInfo: {
                flex: 1,
        },
        adminName: {
                fontSize: 16,
                fontWeight: '600',
                marginBottom: 2,
        },
        adminEmail: {
                fontSize: 12,
        },
        inactiveBadge: {
                paddingHorizontal: 8,
                paddingVertical: 2,
                borderRadius: 10,
                marginLeft: 8,
        },
        inactiveBadgeText: {
                color: '#ffffff',
                fontSize: 10,
                fontWeight: '600',
        },
        adminListArrow: {
                fontSize: 24,
                marginLeft: 8,
        },
        closeButton: {
                padding: 14,
                borderRadius: 8,
                alignItems: 'center',
                marginTop: 8,
        },
        closeButtonText: {
                color: '#ffffff',
                fontSize: 16,
                fontWeight: '600',
        },
        // Admin Detail Modal Styles
        adminDetailHeader: {
                alignItems: 'center',
                marginBottom: 24,
        },
        adminDetailAvatar: {
                width: 60,
                height: 60,
                borderRadius: 30,
                alignItems: 'center',
                justifyContent: 'center',
                marginBottom: 12,
        },
        adminDetailAvatarText: {
                color: '#ffffff',
                fontSize: 20,
                fontWeight: '700',
        },
        adminDetailName: {
                fontSize: 20,
                fontWeight: '700',
                marginBottom: 8,
        },
        contactInfoSection: {
                marginBottom: 16,
        },
        contactLabel: {
                fontSize: 12,
                fontWeight: '600',
                marginBottom: 8,
                textTransform: 'uppercase',
        },
        contactButton: {
                flexDirection: 'row',
                alignItems: 'center',
                padding: 14,
                borderRadius: 8,
        },
        contactButtonIcon: {
                fontSize: 18,
                marginRight: 10,
        },
        contactButtonText: {
                color: '#ffffff',
                fontSize: 14,
                fontWeight: '500',
        },
        noPhoneText: {
                fontSize: 14,
                fontStyle: 'italic',
        },
        adminDetailButtons: {
                flexDirection: 'row',
                gap: 12,
                marginTop: 16,
        },
        backButton: {
                flex: 1,
                padding: 14,
                borderRadius: 8,
                alignItems: 'center',
        },
        backButtonText: {
                color: '#ffffff',
                fontSize: 14,
                fontWeight: '600',
        },
});