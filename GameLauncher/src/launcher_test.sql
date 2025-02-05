CREATE TABLE `games` (
  `id` int(11) NOT NULL,
  `name` varchar(255) DEFAULT NULL,
  `description` varchar(255) DEFAULT NULL,
  `image_path` varchar(255) DEFAULT NULL,
  `download_link` varchar(255) DEFAULT NULL,
  `release_date` datetime DEFAULT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_general_ci;

INSERT INTO `games` (`id`, `name`, `description`, `image_path`, `download_link`, `release_date`) VALUES
(0, 'Top Down Game', 'This is the Grand Theft Auto Series latest games.', 'https://pngimg.com/uploads/gta/gta_PNG13.png', 'https://www.dropbox.com/scl/fi/ldw8hbhpknqjpvhby2oeq/Top-Down-Game.zip?rlkey=47x882ac6mc6i4lt4qofrcsdz&st=2vd07gh3&dl=1', '2011-07-20 08:21:09');

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
  MODIFY `id` int(11) NOT NULL AUTO_INCREMENT, AUTO_INCREMENT=15;
COMMIT;