CREATE TABLE `games` (
  `id` int(11) NOT NULL,
  `name` varchar(255) DEFAULT NULL,
  `description` varchar(255) DEFAULT NULL,
  `image_path` varchar(255) DEFAULT NULL,
  `download_link` varchar(255) DEFAULT NULL,
  `release_date` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT INTO `games` (`id`, `name`, `description`, `image_path`, `download_link`, `release_date`) VALUES
(0, 'GTA V', 'This is the Grand Theft Auto Series latest games.', 'https://pngimg.com/uploads/gta/gta_PNG13.png', 'https://getsamplefiles.com/download/zip/sample-3.zip', '2011-07-20 08:21:09'),
(1, 'pn', '!!444!', NULL, 'https://drive.google.com/uc?export=download&id=1ssm-N-FUhn-ei2YdHTfLTsR4OcdDf97s', '2025-01-20 11:36:56');

CREATE TABLE `users` (
  `id` int(11) NOT NULL,
  `username` varchar(255) DEFAULT NULL,
  `email` varchar(255) DEFAULT NULL,
  `salt` varchar(255) DEFAULT NULL,
  `password_hash` varchar(255) DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT INTO `users` (`id`, `username`, `email`, `salt`, `password_hash`) VALUES
(10, 'admin', 'admin@gmail.com', 'Tql7h/wgJ0MPoMTSNsahBA==', 'aoboGShX2upvRfFBIiFMRWZ4gqOBDlJufHjuGR2DYeI=');

ALTER TABLE `games`
  ADD PRIMARY KEY (`id`);

ALTER TABLE `users`
  ADD PRIMARY KEY (`id`);

ALTER TABLE `users`
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=11;
COMMIT;