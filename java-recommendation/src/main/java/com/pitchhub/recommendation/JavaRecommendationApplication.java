package com.pitchhub.recommendation;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.autoconfigure.domain.EntityScan;
import org.springframework.data.jpa.repository.config.EnableJpaRepositories;

@SpringBootApplication(scanBasePackages = "com.pitchhub")
@EntityScan(basePackages = "com.pitchhub")
@EnableJpaRepositories(basePackages = "com.pitchhub")
public class JavaRecommendationApplication {

	public static void main(String[] args) {
		SpringApplication.run(JavaRecommendationApplication.class, args);
	}

}
