CREATE TABLE `users` (
  `id` INT PRIMARY KEY,
  `username` VARCHAR(255),
  `email` VARCHAR(255),
  `salt` varchar(255),
  `password_hash` VARCHAR(255)
);
