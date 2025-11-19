-- Add ApkDownloadUrl column to LaundrySettings table
-- Run this SQL script in your MySQL database

USE `laundry_db`;

-- Check if column exists, if not add it
ALTER TABLE `LaundrySettings`
ADD COLUMN IF NOT EXISTS `ApkDownloadUrl` longtext CHARACTER SET utf8mb4 NULL;

-- Verify the column was added
DESCRIBE `LaundrySettings`;
